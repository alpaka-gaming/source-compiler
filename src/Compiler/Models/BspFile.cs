using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ValveKeyValue;

namespace SourceSDK.Models
{
    public class BspFile : IDisposable
    {
        private FileStream _bsp;
        private bool _isL4D2;
        private KeyValuePair<int, int>[] _offsets; // offset/length
        private BinaryReader _reader;
        private FileInfo _fileInfo;

        public FileInfo File => _fileInfo;

        private string _gameFolder;

        private Dictionary<string, List<string>> keys;
        public BspFile()
        {
            keys = new Dictionary<string, List<string>>();
            keys.Add("vmfSoundKeys", new List<string>());
            keys.Add("vmfModelKeys", new List<string>());
            keys.Add("vmfMaterialKeys", new List<string>());
            keys.Add("vmtTextureKeyWords", new List<string>());
            keys.Add("vmtMaterialKeyWords", new List<string>());
        }
        public BspFile(string path, string gameFolder) : this()
        {
            _fileInfo = new FileInfo(path);
            _gameFolder = gameFolder;
        }

        public bool RenameNav { get; set; }
        public bool GenParticleManifest { get; set; }

        public void Read()
        {
            Read(_fileInfo.FullName, _gameFolder);
        }

        public void Read(string path, string gameFolder)
        {
            _fileInfo = new FileInfo(path);
            _gameFolder = gameFolder;
            _offsets = new KeyValuePair<int, int>[64];
            using (_bsp = new FileStream(_fileInfo.FullName, FileMode.Open))
            using (_reader = new BinaryReader(_bsp))
            {
                _bsp.Seek(4, SeekOrigin.Begin); //skip header
                var bspVer = _reader.ReadInt32();

                //hack for detecting l4d2 maps
                if (_reader.ReadInt32() == 0 && bspVer == 21)
                {
                    _isL4D2 = true;
                }

                // reset reader position
                _bsp.Seek(-4, SeekOrigin.Current);

                //gathers an array of offsets (where things are located in the bsp)
                for (var i = 0; i < _offsets.GetLength(0); i++)
                {
                    // l4d2 has different lump order
                    if (_isL4D2)
                    {
                        _bsp.Seek(4, SeekOrigin.Current); //skip version
                        _offsets[i] = new KeyValuePair<int, int>(_reader.ReadInt32(), _reader.ReadInt32());
                        _bsp.Seek(4, SeekOrigin.Current); //skip id
                    }
                    else
                    {
                        _offsets[i] = new KeyValuePair<int, int>(_reader.ReadInt32(), _reader.ReadInt32());
                        _bsp.Seek(8, SeekOrigin.Current); //skip id and version
                    }
                }

                buildEntityList();

                buildEntModelList();
                buildModelList();

                buildParticleList();

                buildEntTextureList();
                buildTextureList();

                buildEntSoundList();

                // var gameinfoFile = Path.Combine(gameFolder, "gameinfo.txt");
                // if (!File.Exists(gameinfoFile)) throw new FileNotFoundException("Unable to locate gameinfo.txt");
                //
                //
                // KVObject data = null;
                // try
                // {
                //     using (var stream = File.OpenRead(gameinfoFile))
                //     {
                //         var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                //         var options = new KVSerializerOptions
                //         {
                //             HasEscapeSequences = true
                //         };
                //         data = kv.Deserialize(stream, options);
                //     }
                // }
                // catch (Exception)
                // {
                //     // ignored
                // }
                // var sourceDirectories = data.

                findBspUtilityFiles(new List<string>(new[]
                {
                    gameFolder
                }), RenameNav, GenParticleManifest);
            }
        }

        public List<Dictionary<string, string>> entityList { get; private set; }

        public List<List<Tuple<string, string>>> entityListArrayForm { get; private set; }

        public List<int>[] modelSkinList { get; private set; }

        public List<string> ModelList { get; private set; }

        public List<string> EntModelList { get; private set; }

        public List<string> ParticleList { get; private set; }

        public List<string> TextureList { get; private set; }
        public List<string> EntTextureList { get; private set; }

        public List<string> EntSoundList { get; private set; }

        // key/values as internalPath/externalPath
        private KeyValuePair<string, string> particleManifest { get; set; }
        private KeyValuePair<string, string> soundscript { get; set; }
        private KeyValuePair<string, string> soundscape { get; set; }
        private KeyValuePair<string, string> detail { get; set; }
        private KeyValuePair<string, string> nav { get; set; }
        private KeyValuePair<string, string> res { get; set; }
        private KeyValuePair<string, string> kv { get; set; }
        private KeyValuePair<string, string> txt { get; set; }
        private KeyValuePair<string, string> jpg { get; set; }
        private KeyValuePair<string, string> radartxt { get; set; }
        private List<KeyValuePair<string, string>> radardds { get; set; }
        private List<KeyValuePair<string, string>> languages { get; set; }
        private List<KeyValuePair<string, string>> VehicleScriptList { get; set; }
        private List<KeyValuePair<string, string>> EffectScriptList { get; set; }
        private List<string> vscriptList { get; set; }
        private List<KeyValuePair<string, string>> PanoramaMapIcons { get; set; }


        public void buildEntityList()
        {
            entityList = new List<Dictionary<string, string>>();
            entityListArrayForm = new List<List<Tuple<string, string>>>();

            _bsp.Seek(_offsets[0].Key, SeekOrigin.Begin);
            var ent = _reader.ReadBytes(_offsets[0].Value);
            var ents = new List<byte>();

            const int LCURLY = 123;
            const int RCURLY = 125;
            const int NEWLINE = 10;

            for (var i = 0; i < ent.Length; i++)
            {
                if (ent[i] == LCURLY && i + 1 < ent.Length)
                {
                    // if curly isnt followed by newline assume its part of filename
                    if (ent[i + 1] != NEWLINE)
                    {
                        ents.Add(ent[i]);
                    }
                }
                if (ent[i] != LCURLY && ent[i] != RCURLY)
                {
                    ents.Add(ent[i]);
                }
                else if (ent[i] == RCURLY)
                {
                    // if curly isnt followed by newline assume its part of filename
                    if (i + 1 < ent.Length && ent[i + 1] != NEWLINE)
                    {
                        ents.Add(ent[i]);
                        continue;
                    }


                    var rawent = Encoding.ASCII.GetString(ents.ToArray());
                    var entity = new Dictionary<string, string>();
                    var entityArrayFormat = new List<Tuple<string, string>>();
                    // split on \n, ignore \n inside of quotes
                    foreach (var s in Regex.Split(rawent, "(?=(?:(?:[^\"]*\"){2})*[^\"]*$)\\n"))
                    {
                        if (s.Count() != 0)
                        {
                            var c = s.Split('"');
                            if (!entity.ContainsKey(c[1]))
                            {
                                entity.Add(c[1], c[3]);
                            }
                            entityArrayFormat.Add(Tuple.Create(c[1], c[3]));
                        }
                    }
                    entityList.Add(entity);
                    entityListArrayForm.Add(entityArrayFormat);
                    ents = new List<byte>();
                }
            }
        }

        public void buildTextureList()
        {
            // builds the list of textures applied to brushes

            var mapname = _bsp.Name.Split('\\').Last().Split('.')[0];

            TextureList = new List<string>();
            _bsp.Seek(_offsets[43].Key, SeekOrigin.Begin);
            TextureList = new List<string>(Encoding.ASCII.GetString(_reader.ReadBytes(_offsets[43].Value)).Split('\0'));
            for (var i = 0; i < TextureList.Count; i++)
            {
                if (TextureList[i].StartsWith("/")) // materials in root level material directory start with /
                {
                    TextureList[i] = "materials" + TextureList[i] + ".vmt";
                }
                else
                {
                    TextureList[i] = "materials/" + TextureList[i] + ".vmt";
                }
            }

            // find skybox materials
            var worldspawn = entityList.First(item => item["classname"] == "worldspawn");
            if (worldspawn.ContainsKey("skyname"))
            {
                foreach (var s in new[]
                {
                    "bk", "dn", "ft", "lf", "rt", "up"
                })
                {
                    TextureList.Add("materials/skybox/" + worldspawn["skyname"] + s + ".vmt");
                    TextureList.Add("materials/skybox/" + worldspawn["skyname"] + "_hdr" + s + ".vmt");
                }
            }

            // find detail materials
            if (worldspawn.ContainsKey("detailmaterial"))
            {
                TextureList.Add("materials/" + worldspawn["detailmaterial"] + ".vmt");
            }

            // find menu photos
            TextureList.Add("materials/vgui/maps/menu_photos_" + mapname + ".vmt");
        }

        public void buildEntTextureList()
        {
            // builds the list of textures referenced in entities

            EntTextureList = new List<string>();
            foreach (var ent in entityList)
            {
                var materials = new List<string>();
                foreach (var prop in ent)
                {
                    //Console.WriteLine(prop.Key + ": " + prop.Value);
                    if (keys["vmfMaterialKeys"].Contains(prop.Key.ToLower()))
                    {
                        materials.Add(prop.Value);
                        if (prop.Key.ToLower().StartsWith("team_icon"))
                        {
                            materials.Add(prop.Value + "_locked");
                        }
                    }
                }


                // special condition for sprites
                if (ent["classname"].Contains("sprite") && ent.ContainsKey("model"))
                {
                    materials.Add(ent["model"]);
                }

                // special condition for item_teamflag
                if (ent["classname"].Contains("item_teamflag"))
                {
                    if (ent.ContainsKey("flag_trail"))
                    {
                        materials.Add("effects/" + ent["flag_trail"]);
                        materials.Add("effects/" + ent["flag_trail"] + "_red");
                        materials.Add("effects/" + ent["flag_trail"] + "_blu");
                    }
                    if (ent.ContainsKey("flag_icon"))
                    {
                        materials.Add("vgui/" + ent["flag_icon"]);
                        materials.Add("vgui/" + ent["flag_icon"] + "_red");
                        materials.Add("vgui/" + ent["flag_icon"] + "_blu");
                    }
                }

                // special condition for env_funnel. Hardcoded to use sprites/flare6.vmt
                if (ent["classname"].Contains("env_funnel"))
                {
                    materials.Add("sprites/flare6.vmt");
                }

                // special condition for env_embers. Hardcoded to use particle/fire.vmt
                if (ent["classname"].Contains("env_embers"))
                {
                    materials.Add("particle/fire.vmt");
                }

                // special condition for vgui_slideshow_display. directory paramater references all textures in a folder (does not include subfolders)
                if (ent["classname"].Contains("vgui_slideshow_display"))
                {
                    if (ent.ContainsKey("directory"))
                    {
                        var directory = $"{_gameFolder}/materials/vgui/{ent["directory"]}";
                        if (Directory.Exists(directory))
                        {
                            foreach (var file in Directory.GetFiles(directory))
                            {
                                if (file.EndsWith(".vmt"))
                                {
                                    materials.Add($"/vgui/{ent["directory"]}/{Path.GetFileName(file)}");
                                }
                            }
                        }


                    }
                }

                // format and add materials
                foreach (var material in materials)
                {
                    var materialpath = material;
                    if (!material.EndsWith(".vmt") && !materialpath.EndsWith(".spr"))
                    {
                        materialpath += ".vmt";
                    }

                    EntTextureList.Add("materials/" + materialpath);
                }
            }

            // get all overlay mats
            var uniqueMats = new HashSet<string>();
            foreach (var ent in entityListArrayForm)
            {
                foreach (var kv in ent)
                {
                    var match = Regex.Match(kv.Item2, @"r_screenoverlay ([^,]+),");
                    if (match.Success)
                    {
                        uniqueMats.Add(match.Groups[1].Value.Replace(".vmt", ""));
                    }
                }
            }

            foreach (var mat in uniqueMats)
            {
                var path = string.Format("materials/{0}.vmt", mat);
                EntTextureList.Add(path);
            }
        }

        public void buildModelList()
        {
            // builds the list of models that are from prop_static

            ModelList = new List<string>();
            // getting information on the gamelump
            var propStaticId = 0;
            _bsp.Seek(_offsets[35].Key, SeekOrigin.Begin);
            var GameLumpOffsets = new KeyValuePair<int, int>[_reader.ReadInt32()]; // offset/length
            for (var i = 0; i < GameLumpOffsets.Length; i++)
            {
                if (_reader.ReadInt32() == 1936749168)
                {
                    propStaticId = i;
                }
                _bsp.Seek(4, SeekOrigin.Current); //skip flags and version
                GameLumpOffsets[i] = new KeyValuePair<int, int>(_reader.ReadInt32(), _reader.ReadInt32());
            }

            // reading model names from game lump
            _bsp.Seek(GameLumpOffsets[propStaticId].Key, SeekOrigin.Begin);
            var modelCount = _reader.ReadInt32();
            for (var i = 0; i < modelCount; i++)
            {
                var model = Encoding.ASCII.GetString(_reader.ReadBytes(128)).Trim('\0');
                if (model.Length != 0)
                {
                    ModelList.Add(model);
                }
            }

            // from now on we have models, now we want to know what skins they use

            // skipping leaf lump
            var leafCount = _reader.ReadInt32();
            _bsp.Seek(leafCount * 2, SeekOrigin.Current);

            // reading staticprop lump

            var propCount = _reader.ReadInt32();

            //dont bother if there's no props, avoid a dividebyzero exception.
            if (propCount <= 0)
            {
                return;
            }

            var propOffset = _bsp.Position;
            var byteLength = GameLumpOffsets[propStaticId].Key + GameLumpOffsets[propStaticId].Value - (int)propOffset;
            var propLength = byteLength / propCount;

            modelSkinList = new List<int>[modelCount]; // stores the ids of used skins

            for (var i = 0; i < modelCount; i++)
            {
                modelSkinList[i] = new List<int>();
            }

            for (var i = 0; i < propCount; i++)
            {
                _bsp.Seek(i * propLength + propOffset + 24, SeekOrigin.Begin); // 24 skips origin and angles
                int modelId = _reader.ReadUInt16();
                _bsp.Seek(6, SeekOrigin.Current);
                var skin = _reader.ReadInt32();

                if (modelSkinList[modelId].IndexOf(skin) == -1)
                {
                    modelSkinList[modelId].Add(skin);
                }
            }

        }

        public void buildEntModelList()
        {
            // builds the list of models referenced in entities

            EntModelList = new List<string>();
            foreach (var ent in entityList)
            {
                foreach (var prop in ent)
                {
                    if (ent["classname"].StartsWith("func"))
                    {
                        if (prop.Key == "gibmodel")
                        {
                            EntModelList.Add(prop.Value);
                        }
                    }
                    else if (!ent["classname"].StartsWith("trigger") &&
                             !ent["classname"].Contains("sprite"))
                    {
                        if (keys["vmfModelKeys"].Contains(prop.Key))
                        {
                            EntModelList.Add(prop.Value);
                        }
                        // item_sodacan is hardcoded to models/can.mdl
                        // env_beverage spawns item_sodacans
                        else if (prop.Value == "item_sodacan" || prop.Value == "env_beverage")
                        {
                            EntModelList.Add("models/can.mdl");
                        }
                        // tf_projectile_throwable is hardcoded to  models/props_gameplay/small_loaf.mdl
                        else if (prop.Value == "tf_projectile_throwable")
                        {
                            EntModelList.Add("models/props_gameplay/small_loaf.mdl");
                        }
                    }
                }
            }
        }

        public void buildEntSoundList()
        {
            // builds the list of sounds referenced in entities
            char[] special_caracters =
            {
                '*', '#', '@', '>', '<', '^', '(', ')', '}', '$', '!', '?', ' '
            };
            EntSoundList = new List<string>();
            foreach (var ent in entityList)
            {
                foreach (var prop in ent)
                {
                    if (keys["vmfSoundKeys"].Contains(prop.Key.ToLower()))
                    {
                        EntSoundList.Add("sound/" + prop.Value.Trim(special_caracters));
                    }
                    //Pack I/O triggered sounds
                    else if (prop.Value.Contains("PlayVO"))
                    {
                        //Parameter value following PlayVO is always either a sound path or an empty string
                        var io = prop.Value.Split(',').ToList();
                        if (!string.IsNullOrWhiteSpace(io[io.IndexOf("PlayVO") + 1]))
                        {
                            EntSoundList.Add("sound/" + io[io.IndexOf("PlayVO") + 1].Trim(special_caracters));
                        }
                    }
                    else if (prop.Value.Contains("playgamesound"))
                    {
                        var io = prop.Value.Split(',').ToList();
                        if (!string.IsNullOrWhiteSpace(io[io.IndexOf("playgamesound") + 1]))
                        {
                            EntSoundList.Add("sound/" + io[io.IndexOf("playgamesound") + 1].Trim(special_caracters));
                        }
                    }
                    else if (prop.Value.Contains("play"))
                    {
                        var io = prop.Value.Split(',').ToList();

                        var playCommand = io.Where(i => i.StartsWith("play "));

                        foreach (var command in playCommand)
                        {
                            EntSoundList.Add("sound/" + command.Split(' ')[1].Trim(special_caracters));
                        }
                    }

                }
            }


        }

        public void buildParticleList()
        {
            ParticleList = new List<string>();
            foreach (var ent in entityList)
            {
                foreach (var particle in ent)
                {
                    if (particle.Key.ToLower() == "effect_name")
                    {
                        ParticleList.Add(particle.Value);
                    }
                }
            }
        }

        #region IDisposable


        private void ReleaseUnmanagedResources()
        {
            // TODO release unmanaged resources here
        }
        protected virtual void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            if (disposing)
            {
                _bsp?.Dispose();
                _reader?.Dispose();
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~BspFile()
        {
            Dispose(false);
        }


        #endregion

        private List<string> findVmtMaterials(string fullpath)
        {
            // finds vmt files associated with vmt file

            var vmtList = new List<string>();
            foreach (var line in System.IO.File.ReadAllLines(fullpath))
            {
                var param = line.Replace("\"", " ").Replace("\t", " ").Trim();
                if (keys["vmtMaterialKeyWords"].Any(key => param.StartsWith(key + " ")))
                {
                    vmtList.Add("materials/" + vmtPathParser2(line) + ".vmt");
                }
            }
            return vmtList;
        }

        private string vmtPathParser2(string vmtline)
        {
            vmtline = vmtline.Trim(' ', '\t');

            // remove key
            if (vmtline[0] == '"')
            {
                vmtline = Regex.Match(vmtline, "\"[^\"]+\"(.*)$").Groups[1].Value;
            }
            else
            {
                vmtline = Regex.Match(vmtline, "[^ \t]+(.*)$").Groups[1].Value;
            }

            vmtline = vmtline.TrimStart(' ', '\t');
            // process value
            if (vmtline[0] == '"')
            {
                vmtline = Regex.Match(vmtline, "\"([^\"]+)\"").Groups[1].Value;
            }
            else
            {
                // strip c style comments like this one
                var commentIndex = vmtline.IndexOf("//");
                if (commentIndex > -1)
                {
                    vmtline = vmtline.Substring(0, commentIndex);
                }
                vmtline = Regex.Match(vmtline, "[^ \t]+").Groups[0].Value;
            }

            vmtline = vmtline.Trim(' ', '/', '\\'); // removes leading slashes
            vmtline = vmtline.Replace('\\', '/'); // normalize slashes
            vmtline = Regex.Replace(vmtline, "/+", "/"); // remove duplicate slashes

            if (vmtline.StartsWith("materials/"))
            {
                vmtline = vmtline.Remove(0, "materials/".Length); // removes materials/ if its the beginning of the string for consistency
            }
            if (vmtline.EndsWith(".vmt") || vmtline.EndsWith(".vtf")) // removes extentions if present for consistency
            {
                vmtline = vmtline.Substring(0, vmtline.Length - 4);
            }
            return vmtline;
        }

        private void findBspUtilityFiles(List<string> sourceDirectories, bool renamenav, bool genparticlemanifest)
        {
            // Utility files are other files that are not assets and are sometimes not referenced in the bsp
            // those are manifests, soundscapes, nav, radar and detail files
            var bsp = this;

            // Soundscape file
            var internalPath = "scripts/soundscapes_" + bsp._fileInfo.Name.Replace(".bsp", ".txt");
            // Soundscapes can have either .txt or .vsc extensions
            var internalPathVsc = "scripts/soundscapes_" + bsp._fileInfo.Name.Replace(".bsp", ".vsc");
            foreach (var source in sourceDirectories)
            {
                var externalPath = source + "/" + internalPath;
                var externalVscPath = source + "/" + internalPathVsc;

                if (System.IO.File.Exists(externalPath))
                {
                    bsp.soundscape = new KeyValuePair<string, string>(internalPath, externalPath);
                    break;
                }
                if (System.IO.File.Exists(externalVscPath))
                {
                    bsp.soundscape = new KeyValuePair<string, string>(internalPathVsc, externalVscPath);
                    break;
                }
            }

            // Soundscript file
            internalPath = "maps/" + bsp._fileInfo.Name.Replace(".bsp", "") + "_level_sounds.txt";
            foreach (var source in sourceDirectories)
            {
                var externalPath = source + "/" + internalPath;

                if (System.IO.File.Exists(externalPath))
                {
                    bsp.soundscript = new KeyValuePair<string, string>(internalPath, externalPath);
                    break;
                }
            }

            // Nav file (.nav)
            internalPath = "maps/" + bsp._fileInfo.Name.Replace(".bsp", ".nav");
            foreach (var source in sourceDirectories)
            {
                var externalPath = source + "/" + internalPath;

                if (System.IO.File.Exists(externalPath))
                {
                    if (renamenav)
                    {
                        internalPath = "maps/embed.nav";
                    }
                    bsp.nav = new KeyValuePair<string, string>(internalPath, externalPath);
                    break;
                }
            }

            // detail file (.vbsp)
            var worldspawn = bsp.entityList.First(item => item["classname"] == "worldspawn");
            if (worldspawn.ContainsKey("detailvbsp"))
            {
                internalPath = worldspawn["detailvbsp"];

                foreach (var source in sourceDirectories)
                {
                    var externalPath = source + "/" + internalPath;

                    if (System.IO.File.Exists(externalPath))
                    {
                        bsp.detail = new KeyValuePair<string, string>(internalPath, externalPath);
                        break;
                    }
                }
            }


            // Vehicle scripts
            var vehicleScripts = new List<KeyValuePair<string, string>>();
            foreach (var ent in bsp.entityList)
            {
                if (ent.ContainsKey("vehiclescript"))
                {
                    foreach (var source in sourceDirectories)
                    {
                        var externalPath = source + "/" + ent["vehiclescript"];
                        if (System.IO.File.Exists(externalPath))
                        {
                            internalPath = ent["vehiclescript"];
                            vehicleScripts.Add(new KeyValuePair<string, string>(ent["vehiclescript"], externalPath));
                        }
                    }
                }
            }
            bsp.VehicleScriptList = vehicleScripts;

            // Effect Scripts
            var effectScripts = new List<KeyValuePair<string, string>>();
            foreach (var ent in bsp.entityList)
            {
                if (ent.ContainsKey("scriptfile"))
                {
                    foreach (var source in sourceDirectories)
                    {
                        var externalPath = source + "/" + ent["scriptfile"];
                        if (System.IO.File.Exists(externalPath))
                        {
                            internalPath = ent["scriptfile"];
                            effectScripts.Add(new KeyValuePair<string, string>(ent["scriptfile"], externalPath));
                        }
                    }
                }
            }
            bsp.EffectScriptList = effectScripts;

            // Res file (for tf2's pd gamemode)
            var pd_ent = bsp.entityList.FirstOrDefault(item => item["classname"] == "tf_logic_player_destruction");
            if (pd_ent != null && pd_ent.ContainsKey("res_file"))
            {
                foreach (var source in sourceDirectories)
                {
                    var externalPath = source + "/" + pd_ent["res_file"];
                    if (System.IO.File.Exists(externalPath))
                    {
                        bsp.res = new KeyValuePair<string, string>(pd_ent["res_file"], externalPath);
                        break;
                    }
                }
            }

            // Radar file
            internalPath = "resource/overviews/" + bsp._fileInfo.Name.Replace(".bsp", ".txt");
            var ddsfiles = new List<KeyValuePair<string, string>>();
            foreach (var source in sourceDirectories)
            {
                var externalPath = source + "/" + internalPath;

                if (System.IO.File.Exists(externalPath))
                {
                    bsp.radartxt = new KeyValuePair<string, string>(internalPath, externalPath);
                    bsp.TextureList.AddRange(findVmtMaterials(externalPath));

                    var ddsInternalPaths = findRadarDdsFiles(externalPath);
                    //find out if they exists or not
                    foreach (var ddsInternalPath in ddsInternalPaths)
                    {
                        foreach (var source2 in sourceDirectories)
                        {
                            var ddsExternalPath = source2 + "/" + ddsInternalPath;
                            if (System.IO.File.Exists(ddsExternalPath))
                            {
                                ddsfiles.Add(new KeyValuePair<string, string>(ddsInternalPath, ddsExternalPath));
                                break;
                            }
                        }
                    }
                    break;
                }
            }
            bsp.radardds = ddsfiles;

            // csgo kv file (.kv)
            internalPath = "maps/" + bsp._fileInfo.Name.Replace(".bsp", ".kv");
            foreach (var source in sourceDirectories)
            {
                var externalPath = source + "/" + internalPath;

                if (System.IO.File.Exists(externalPath))
                {
                    bsp.kv = new KeyValuePair<string, string>(internalPath, externalPath);
                    break;
                }
            }

            // csgo loading screen text file (.txt)
            internalPath = "maps/" + bsp._fileInfo.Name.Replace(".bsp", ".txt");
            foreach (var source in sourceDirectories)
            {
                var externalPath = source + "/" + internalPath;

                if (System.IO.File.Exists(externalPath))
                {
                    bsp.txt = new KeyValuePair<string, string>(internalPath, externalPath);
                    break;
                }
            }

            // csgo loading screen image (.jpg)
            internalPath = "maps/" + bsp._fileInfo.Name.Replace(".bsp", "");
            foreach (var source in sourceDirectories)
            {
                var externalPath = source + "/" + internalPath;

                foreach (var extension in new[]
                {
                    ".jpg", ".jpeg"
                })
                {
                    if (System.IO.File.Exists(externalPath + extension))
                    {
                        bsp.jpg = new KeyValuePair<string, string>(internalPath + ".jpg", externalPath + extension);
                    }
                }
            }

            // csgo panorama map icons (.png)
            internalPath = "materials/panorama/images/map_icons/screenshots/";
            var panoramaMapIcons = new List<KeyValuePair<string, string>>();
            foreach (var source in sourceDirectories)
            {
                var externalPath = source + "/" + internalPath;
                var bspName = bsp._fileInfo.Name.Replace(".bsp", "");

                foreach (var resolution in new[]
                {
                    "360p", "1080p"
                })
                {
                    if (System.IO.File.Exists($"{externalPath}{resolution}/{bspName}.png"))
                    {
                        panoramaMapIcons.Add(new KeyValuePair<string, string>($"{internalPath}{resolution}/{bspName}.png", $"{externalPath}{resolution}/{bspName}.png"));
                    }
                }
            }
            bsp.PanoramaMapIcons = panoramaMapIcons;

            // language files, particle manifests and soundscript file
            // (these language files are localized text files for tf2 mission briefings)
            var internalDir = "maps/";
            var name = bsp._fileInfo.Name.Replace(".bsp", "");
            var searchPattern = name + "*.txt";
            var langfiles = new List<KeyValuePair<string, string>>();

            foreach (var source in sourceDirectories)
            {
                var externalDir = source + "/" + internalDir;
                var dir = new DirectoryInfo(externalDir);

                if (dir.Exists)
                {
                    foreach (var f in dir.GetFiles(searchPattern))
                    {
                        // particle files if particle manifest is not being generated
                        if (f.Name.StartsWith(name + "_particles") || f.Name.StartsWith(name + "_manifest"))
                        {
                            if (!genparticlemanifest)
                            {
                                bsp.particleManifest = new KeyValuePair<string, string>(internalDir + f.Name, externalDir + f.Name);
                            }
                            continue;
                        }
                        // soundscript
                        if (f.Name.StartsWith(name + "_level_sounds"))
                        {
                            bsp.soundscript =
                                new KeyValuePair<string, string>(internalDir + f.Name, externalDir + f.Name);
                        }
                        // presumably language files
                        else
                        {
                            langfiles.Add(new KeyValuePair<string, string>(internalDir + f.Name, externalDir + f.Name));
                        }
                    }
                }
            }
            bsp.languages = langfiles;

            // ASW/Source2009 branch VScripts
            var vscripts = new List<string>();

            foreach (var entity in bsp.entityList)
            {
                foreach (var kvp in entity)
                {
                    if (kvp.Key.ToLower() == "vscripts")
                    {
                        var scripts = kvp.Value.Split(' ');
                        foreach (var script in scripts)
                        {
                            vscripts.Add("scripts/vscripts/" + script);
                        }
                    }
                }
            }
            bsp.vscriptList = vscripts;
        }

        private List<string> findRadarDdsFiles(string fullpath)
        {
            // finds vmt files associated with radar overview files

            var DDSs = new List<string>();
            var overviewFile = new FileData(fullpath);

            // Contains no blocks, return empty list
            if (overviewFile.headnode.subBlocks.Count == 0)
            {
                return DDSs;
            }

            foreach (var subblock in overviewFile.headnode.subBlocks)
            {
                var material = subblock.TryGetStringValue("material");
                // failed to get material, file contains no materials
                if (material == "")
                {
                    break;
                }

                // add default radar
                DDSs.Add($"resource/{vmtPathParser(material, false)}_radar.dds");

                var verticalSections = subblock.GetFirstByName("\"verticalsections\"");
                if (verticalSections == null)
                {
                    break;
                }

                // add multi-level radars
                foreach (var section in verticalSections.subBlocks)
                {
                    DDSs.Add($"resource/{vmtPathParser(material, false)}_{section.name.Replace("\"", string.Empty)}_radar.dds");
                }
            }

            return DDSs;
        }

        private string vmtPathParser(string vmtline, bool needsSplit = true)
        {
            if (needsSplit)
            {
                vmtline = vmtline.Split(new[]
                {
                    ' '
                }, 2)[1]; // removes the parameter name
            }
            vmtline = vmtline.Split(new[]
            {
                "//", "\\\\"
            }, StringSplitOptions.None)[0]; // removes endline parameter
            vmtline = vmtline.Trim(' ', '/', '\\'); // removes leading slashes
            vmtline = vmtline.Replace('\\', '/'); // normalize slashes
            if (vmtline.StartsWith("materials/"))
            {
                vmtline = vmtline.Remove(0, "materials/".Length); // removes materials/ if its the beginning of the string for consistency
            }
            if (vmtline.EndsWith(".vmt") || vmtline.EndsWith(".vtf")) // removes extentions if present for consistency
            {
                vmtline = vmtline.Substring(0, vmtline.Length - 4);
            }
            return vmtline;
        }

    }
}

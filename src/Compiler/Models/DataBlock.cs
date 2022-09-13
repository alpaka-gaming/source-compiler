using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SourceSDK.Models
{
   public class DataBlock
    {
        public string name = "";
        public List<DataBlock> subBlocks = new List<DataBlock>();
        public Dictionary<string, string> values = new Dictionary<string, string>();
        
        private static string GetUnquotedMaterial(string quoted)
        {
            var sgts = quoted.Split('\"');
            var unquoted = "";
            var i = 0;
            foreach (var s in sgts)
            {
                if (i++ % 2 != 0)
                {
                    continue;
                }
                unquoted += s;
            }
            return unquoted;
        }

        private static DataBlock ParseDataBlock(ref StringReader reader, string name = "")
        {
            var block = new DataBlock
            {
                name = name.Trim()
            };

            string line;
            var prev = "";
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Split(new[]
                {
                    "//"
                }, StringSplitOptions.None)[0]; // ditch comments

                if (GetUnquotedMaterial(line).Contains("{"))
                {
                    var pname = prev;
                    //prev = prev.Replace("\"", "");
                    block.subBlocks.Add(ParseDataBlock(ref reader, pname));
                }
                if (GetUnquotedMaterial(line).Contains("}"))
                {
                    return block;
                }

                var regex = new Regex("\"(.*?)\"|([^\\s]+)", RegexOptions.Compiled);
                var _matches = regex.Matches(line);
                var strings = new List<string>();

                for (var i = 0; i < _matches.Count; i++)
                {
                    strings.Add(_matches[i].Value.Replace("\"", ""));
                }

                if (strings.Count == 2)
                {
                    var keyname = strings[0];
                    var i = -1;
                    while (block.values.ContainsKey(++i > 0 ? keyname + i : keyname))
                    {
                        ;
                    }
                    block.values[i > 0 ? keyname + i : keyname] = strings[1];
                }

                prev = line;
            }

            return block;
        }

        public static DataBlock FromString(string block, string name = "")
        {
            var reader = new StringReader(block);
            return ParseDataBlock(ref reader, name);
        }

        public static DataBlock FromStream(ref StreamReader stream, string name = "")
        {
            var reader = new StringReader(stream.ReadToEnd());
            return ParseDataBlock(ref reader, name);
        }

        public void Serialize(ref StreamWriter stream, int depth = 0)
        {
            var indenta = "";
            for (var i = 0; i < depth; i++)
            {
                indenta += "\t";
            }
            var indentb = indenta + "\t";

            if (depth >= 0)
            {
                stream.Write(indenta + name + "\n" + indenta + "{\n");
            }

            foreach (var key in values.Keys)
            {
                stream.Write(indentb + "\"" + key + "\" \"" + values[key] + "\"\n");
            }

            for (var i = 0; i < subBlocks.Count; i++)
            {
                subBlocks[i].Serialize(ref stream, depth + 1);
            }

            if (depth >= 0)
            {
                stream.Write(indenta + "}\n");
            }
        }

        public DataBlock GetFirstByName(string _name)
        {
            for (var i = 0; i < subBlocks.Count; i++)
            {
                if (_name == subBlocks[i].name)
                {
                    return subBlocks[i];
                }
            }

            return null;
        }
        public DataBlock GetFirstByName(string[] names)
        {
            for (var i = 0; i < subBlocks.Count; i++)
            {
                if (names.Contains(subBlocks[i].name))
                {
                    return subBlocks[i];
                }
            }

            return null;
        }

        public List<DataBlock> GetAllByName(string _name)
        {
            var c = new List<DataBlock>();
            for (var i = 0; i < subBlocks.Count; i++)
            {
                if (_name == subBlocks[i].name)
                {
                    c.Add(subBlocks[i]);
                }
            }

            return c;
        }

        public string TryGetStringValue(string key, string defaultValue = "")
        {
            if (!values.ContainsKey(key))
            {
                return defaultValue;
            }
            return values[key];
        }

        public List<string> GetList(string key)
        {
            var list = new List<string>();
            var vc = -1;
            while (values.ContainsKey(key + (++vc > 0 ? vc.ToString() : "")))
            {
                list.Add(values[key + (vc > 0 ? vc.ToString() : "")]);
            }
            return list;
        }

        public override string ToString()
        {
            return $"DataBlock<\n\tname={name}\n\tvalues={{{string.Join("\n\t\t", values)}}}\n\tsubBlocks=[\n{string.Join(", ", subBlocks)}\n]>";
        }
    }
}

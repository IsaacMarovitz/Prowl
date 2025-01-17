﻿using Prowl.Runtime;
using Prowl.Runtime.Utils;
using System.Text.RegularExpressions;
using static Prowl.Runtime.Shader;

namespace Prowl.Editor.Assets
{
    [Importer("ShaderIcon.png", typeof(Shader), ".shader")]
    public class ShaderImporter : ScriptedImporter
    {
        public static readonly string[] Supported = { ".shader" };

        private static FileInfo currentAssetPath;

#warning TODO: get Uniforms via regex as well, So we know what unifoms the shader has and can skip SetUniforms if they dont have it

        public override void Import(SerializedAsset ctx, FileInfo assetPath)
        {
            // Just confirm the format, We should have todo this but technically someone could call ImportShader manually skipping the existing format check
            if (!Supported.Contains(assetPath.Extension))
            {
                ImGuiNotify.InsertNotification("Failed to Import Shader.", new(0.8f, 0.1f, 0.1f, 1f), "Format Not Supported: " + assetPath.Extension);
                return;
            }

            currentAssetPath = assetPath;

            string shaderScript = File.ReadAllText(assetPath.FullName);

            // Strip out comments and Multi-like Comments
            shaderScript = ClearAllComments(shaderScript);

            // Parse the shader
            var parsedShader = ParseShader(shaderScript);

            // Sort passes to be in order
            parsedShader.Passes = parsedShader.Passes.OrderBy(p => p.Order).ToArray();

            // Now we have a Vertex and Fragment shader will all Includes, and Shared code inserted
            // Now we turn the ParsedShader into a Shader
            Shader shader = new();
            shader.Name = parsedShader.Name;
            shader.Properties = parsedShader.Properties;
            shader.Passes = new();

            for (int i = 0; i < parsedShader.Passes.Length; i++)
            {
                var parsedPass = parsedShader.Passes[i];
                shader.Passes.Add(new ShaderPass
                {
                    RenderMode = parsedPass.RenderMode,
                    Vertex = parsedPass.Vertex,
                    Fragment = parsedPass.Fragment,
                });
            }

            if (parsedShader.ShadowPass != null)
                shader.ShadowPass = new ShaderShadowPass
                {
                    Vertex = parsedShader.ShadowPass.Vertex,
                    Fragment = parsedShader.ShadowPass.Fragment,
                };

            ctx.SetMainObject(shader);


            ImGuiNotify.InsertNotification("Shader Imported.", new(0.75f, 0.35f, 0.20f, 1.00f), assetPath.FullName);
        }

#warning TODO: Replace regex with a proper parser, this works just fine for now though so Low Priority

        public static ParsedShader ParseShader(string input)
        {
            var shader = new ParsedShader
            {
                Name = ParseShaderName(input),
                Properties = ParseProperties(input),
                Passes = ParsePasses(input).ToArray(),
                ShadowPass = ParseShadowPass(input)
            };

            return shader;
        }

        private static string ParseShaderName(string input)
        {
            var match = Regex.Match(input, @"Shader\s+""([^""]+)""");
            if (!match.Success)
                throw new Exception("Malformed input: Missing Shader declaration");
            return match.Groups[1].Value;
        }

        private static List<Property> ParseProperties(string input)
        {
            var propertiesList = new List<Property>();

            var propertiesBlockMatch = Regex.Match(input, @"Properties\s*{([^{}]*?)}", RegexOptions.Singleline);
            if (propertiesBlockMatch.Success)
            {
                var propertiesBlock = propertiesBlockMatch.Groups[1].Value;

                var propertyMatches = Regex.Matches(propertiesBlock, @"(\w+)\s*\(\""([^\""]+)\"".*?,\s*(\w+)");
                foreach (Match match in propertyMatches)
                {
                    var property = new Property
                    {
                        Name = match.Groups[1].Value,
                        DisplayName = match.Groups[2].Value,
                        Type = ParsePropertyType(match.Groups[3].Value)
                    };
                    propertiesList.Add(property);
                }
            }

            return propertiesList;
        }

        private static Property.PropertyType ParsePropertyType(string typeStr)
        {
            try
            {
                return (Property.PropertyType)Enum.Parse(typeof(Property.PropertyType), typeStr, true);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException($"Unknown property type: {typeStr}");
            }
        }

        private static readonly Regex _preprocessorIncludeRegex = new Regex(@"^\s*#include\s*[""<](.+?)["">]\s*$", RegexOptions.Multiline);

        private static List<ParsedShaderPass> ParsePasses(string input)
        {
            var passesList = new List<ParsedShaderPass>();

            var passMatches = Regex.Matches(input, @"\bPass (\d+)\s+({(?:[^{}]|(?<o>{)|(?<-o>}))+(?(o)(?!))})");
            foreach (Match passMatch in passMatches)
            {
                var passContent = passMatch.Groups[2].Value;

                var shaderPass = new ParsedShaderPass
                {
                    Order = int.Parse(passMatch.Groups[1].Value),
                    RenderMode = ParseBlockContent(passContent, "RenderMode"),
                    Vertex = ParseBlockContent(passContent, "Vertex"),
                    Fragment = ParseBlockContent(passContent, "Fragment"),
                };

                shaderPass.Vertex = _preprocessorIncludeRegex.Replace(shaderPass.Vertex, ImportReplacer);
                shaderPass.Fragment = _preprocessorIncludeRegex.Replace(shaderPass.Fragment, ImportReplacer);

                passesList.Add(shaderPass);
            }

            return passesList;
        }

        private static string ImportReplacer(Match match)
        {
            var relativePath = match.Groups[1].Value + ".glsl";

            var combined = Path.Combine(currentAssetPath.Directory!.FullName, relativePath);
            string absolutePath = Path.GetFullPath(combined);
            if (!File.Exists(absolutePath))
            {
                ImGuiNotify.InsertNotification("Failed to Import Shader.", new(0.8f, 0.1f, 0.1f, 1f), "Include not found: " + absolutePath);
                return "";
            }

            // Recursively handle Imports
            var includeScript = _preprocessorIncludeRegex.Replace(File.ReadAllText(absolutePath), ImportReplacer);
            // Strip out comments and Multi-like Comments
            includeScript = ClearAllComments(includeScript);
            return includeScript;
        }

        private static ParsedShaderShadowPass ParseShadowPass(string input)
        {
            var passMatches = Regex.Matches(input, @"ShadowPass (\d+)\s+({(?:[^{}]|(?<o>{)|(?<-o>}))+(?(o)(?!))})");
            foreach (Match passMatch in passMatches)
            {
                var passContent = passMatch.Groups[2].Value;
                var shaderPass = new ParsedShaderShadowPass
                {
                    Vertex = ParseBlockContent(passContent, "Vertex"),
                    Fragment = ParseBlockContent(passContent, "Fragment"),
                };
                return shaderPass; // Just return the first one, any other ones are ignored
            }

            return null; // No shadow pass
        }

        //private static List<string> ParsePassIncludes(string passContent)
        //{
        //    var includes = new List<string>();
        //    var matches = Regex.Matches(passContent, @"Include\s+""([^""]+)""");
        //    foreach (Match match in matches)
        //    {
        //        includes.Add(match.Groups[1].Value);
        //    }
        //    return includes;
        //}

        private static string ParseBlockContent(string input, string blockName)
        {
            var blockMatch = Regex.Match(input, $@"{blockName}\s*({{(?:[^{{}}]|(?<o>{{)|(?<-o>}}))+(?(o)(?!))}})");

            if (blockMatch.Success)
            {
                var content = blockMatch.Groups[1].Value;
                // Strip off the enclosing braces and return
                return content.Substring(1, content.Length - 2).Trim();
            }
            return "";
        }

        public static string ClearAllComments(string input)
        {
            // Remove single-line comments
            var noSingleLineComments = Regex.Replace(input, @"//.*", "");

            // Remove multi-line comments
            var noComments = Regex.Replace(noSingleLineComments, @"/\*.*?\*/", "", RegexOptions.Singleline);

            return noComments;
        }


        public class ParsedShader
        {
            public string Name;
            public List<Property> Properties;
            public ParsedShaderPass[] Passes;
            public ParsedShaderShadowPass ShadowPass;
        }

        public class ParsedShaderPass
        {
            public string RenderMode; // Defaults to Opaque
            public string Vertex;
            public string Fragment;

            public int Order;
        }

        public class ParsedShaderShadowPass
        {
            public string Vertex;
            public string Fragment;
        }

    }
}

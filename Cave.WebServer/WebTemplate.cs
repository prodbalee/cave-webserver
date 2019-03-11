using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Cave.Collections;
using Cave.Collections.Generic;

namespace Cave.Web
{
    /// <summary>
    /// Provides web templates
    /// </summary>
    public class WebTemplate
    {
        /// <summary>The tag to replace</summary>
        public static readonly byte[] Tag = ASCII.GetBytes("<!-- CaveJSON -->");

        /// <summary>The script start data</summary>
        public static readonly byte[] ScriptStart = ASCII.GetBytes("<script type=\"application/javascript\">var CaveJSON=");

        /// <summary>The script end data</summary>
        public static readonly byte[] ScriptEnd = ASCII.GetBytes("</script>");

        class Func
        {
            public string Name;
            public WebServerMethod Method;
            public readonly Dictionary<string, string> Parameters = new Dictionary<string, string>();
            public readonly Set<string> NeededParameters = new Set<string>();
            public WebTemplateParameter[] ParameterDescriptions;
        }

        WebServer Server;
        Func[] Functions;
        WebContentFile[] Content;
        byte[] StaticData;

        void Reload()
        {
            //only one reload at a time
            lock (this)
            {
                { //multi thread reload check
                    DateTime lastChanged = FileSystem.GetLastWriteTimeUtc(FileName);
                    if (lastChanged == LastChanged)
                    {
                        return;
                    }
                }

                Trace.TraceInformation("Reloading template {0}", FileName);

                //read config
                IniReader config = IniReader.FromFile(FileName);
                {
                    LastChanged = FileSystem.GetLastWriteTimeUtc(FileName);
                    int v = config.ReadInt32("CaveWebTemplate", "Version");
                    if (v != 1)
                    {
                        throw new WebServerException(WebError.InternalServerError, 0, $"{FileName} invalid template version!");
                    }
                }

                //build function list
                {
                    List<Func> functions = new List<Func>();
                    foreach (string function in config.ReadSection("Functions", true))
                    {
                        Func f = new Func
                        {
                            Name = function,
                            Method = Server.FindMethod(function)
                        };
                        if (f.Method == null)
                        {
                            throw new WebServerException(WebError.InternalServerError, 0, $"{FileName} invalid function call {function}!");
                        }

                        List<WebTemplateParameter> list = new List<WebTemplateParameter>();
                        foreach (string parameter in config.ReadSection(function, true))
                        {
                            Option i = Option.Parse(parameter);
                            if (i.Name.StartsWith("?"))
                            {
                                switch (i.Name)
                                {
                                    case "?IfParametersPresent":
                                        foreach (string neededParam in i.Value.Split(','))
                                        {
                                            if (neededParam.Trim().Length == 0)
                                            {
                                                continue;
                                            }

                                            f.NeededParameters.Include(neededParam);
                                        }
                                        break;
                                    default: throw new NotImplementedException($"Option {i} is not implemented!");
                                }
                                continue;
                            }
                            f.Parameters.Add(i.Name, i.Value);
                            list.Add(new WebTemplateParameter()
                            {
                                ID = CaveSystemData.CalculateID(function + i.Name),
                                FunctionName = function,
                                ParameterAtFunction = i.Name,
                                ParameterAtTemplate = i.Value,
                            });
                        }
                        f.ParameterDescriptions = list.ToArray();
                        functions.Add(f);
                    }
                    Functions = functions.ToArray();
                }

                //build content
                {
                    List<WebContentFile> content = new List<WebContentFile>();
                    //main file is always first content file
                    string master = config.ReadSetting("CaveWebTemplate", "Master");
                    if (master == null)
                    {
                        master = Path.GetFileNameWithoutExtension(FileName) + ".html";
                    }

                    string folder = Path.GetDirectoryName(FileName);
                    foreach (string contentFile in new string[] { master })
                    {
                        content.Add(new WebContentFile(Server, folder, contentFile));
                    }
                    foreach (string contentFile in config.ReadSection("Content", true))
                    {
                        content.Add(new WebContentFile(Server, folder, contentFile));
                    }

                    if (Server.EnableStaticTemplates)
                    {
                        StaticData = BuildStaticData(content.ToArray());
                    }
                    else
                    {
                        Content = content.ToArray();
                    }
                }
            }
        }

        private byte[] BuildStaticData(WebContentFile[] content)
        {
            byte[] result = null;
            for (int i = 0; i < content.Length; i++)
            {
                WebContentFile item = content[i];
                DateTime lastChanged = FileSystem.GetLastWriteTimeUtc(item.FileName);
                if (lastChanged != item.LastChanged)
                {
                    Trace.TraceInformation("Reloading content {0}", item.LastChanged);
                    string fileFolder = Path.GetDirectoryName(FileName);
                    item = content[i] = new WebContentFile(Server, fileFolder, item.Url);
                }

                if (i == 0)
                {
                    result = item.Content;
                    continue;
                }
                byte[] pattern = ASCII.GetBytes($"<!-- {item.Url} -->");
                result = result.ReplaceFirst(pattern, item.Content);
            }
            return result;
        }

        /// <summary>Initializes a new instance of the <see cref="WebTemplate"/> class.</summary>
        /// <param name="server">The server.</param>
        /// <param name="fileName">Name of the file.</param>
        public WebTemplate(WebServer server, string fileName)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            Server = server;
            FileName = fileName;
            Reload();
        }

        /// <summary>Gets the name of the file.</summary>
        /// <value>The name of the file.</value>
        public string FileName { get; }

        /// <summary>Gets the last changed datetime.</summary>
        /// <value>The last changed datetime.</value>
        public DateTime LastChanged { get; private set; }

        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public string LogSourceName => "CaveWebTemplate";

        /// <summary>Builds the template.</summary>
        /// <param name="data">The data.</param>
        /// <returns>Returns true on success, false otherwise</returns>
        public bool Render(WebData data)
        {
            if (data.Server != Server)
            {
                throw new ArgumentOutOfRangeException(nameof(data.Server));
            }

            { //need reload ?
                DateTime lastChanged = FileSystem.GetLastWriteTimeUtc(FileName);
                if (lastChanged != LastChanged)
                {
                    Reload();
                }
            }
            //do auth and load user session (if any)
            data.Result.SkipMainObject = true;
            data.Result.TransmitLayout = false;

            //call functions
            var templateParameters = data.Request.Parameters;
            var tables = new Set<string>();
            var parameterDescription = new List<WebTemplateParameter>();
            for (int i = 0; i < Functions.Length; i++)
            {
                Func function = Functions[i];
                parameterDescription.AddRange(function.ParameterDescriptions);
                if (function.NeededParameters.Count > 0)
                {
                    //foreach neededparameters, any parameter is not at tmeplate parameters -> continue
                    if (function.NeededParameters.Where(n => !templateParameters.ContainsKey(n)).Any())
                    {
                        continue;
                    }
                }
                Dictionary<string, string> functionParameters = new Dictionary<string, string>();
                foreach (System.Reflection.ParameterInfo methodParameter in function.Method.Parameters)
                {
                    //lookup function parameter name from function section at template
                    if (!function.Parameters.TryGetValue(methodParameter.Name, out string templateParameterName))
                    {
                        continue;
                    }
                    //parameter name at template could be loaded
                    if (!templateParameters.TryGetValue(templateParameterName, out string parameterValue))
                    {
                        if (!methodParameter.IsOptional)
                        {
                            //no value given and is not optional
                            throw new WebServerException(WebError.InvalidParameters, $"Template error: Missing {methodParameter.Name} is not for function {function} is not set. Define {templateParameterName} at template call!");
                        }
                        continue;
                    }
                    functionParameters[methodParameter.Name] = parameterValue;
                }
                data.Request.Parameters = new ReadOnlyDictionary<string, string>(functionParameters);
                //invoke method
                data.Method = function.Method;
                data.Server.CallMethod(data);
            }

            Stopwatch renderWatch = Server.PerformanceChecks ? Stopwatch.StartNew() : null;
            //replace content
            byte[] result = StaticData ?? BuildStaticData(Content);

            if (renderWatch != null)
            {
                Trace.TraceInformation("Template static data generation took {0}", renderWatch.Elapsed.FormatTime());
            }

            //render data 
            {
                data.Result.Type = WebResultType.Json;
                data.Result.AddStructs(parameterDescription);
                WebAnswer answer = data.Result.ToAnswer();
                result = result.ReplaceFirst(Tag, ScriptStart, answer.ContentData, ScriptEnd);
            }
            //set result
            data.Result = null;

            WebMessage message;
            if (data.Method != null)
            {
                message = WebMessage.Create(data.Method, $"Template call <cyan>{data.Request}<default> succeeded.");
            }
            else
            {
                message = WebMessage.Create($"Static {data.Request.PlainUrl}", $"Template call <cyan>{data.Request}<default> succeeded.");
            }

            data.Answer = WebAnswer.Raw(data.Request, message, result, "text/html");
            return true;
        }
    }
}

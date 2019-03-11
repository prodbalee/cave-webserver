using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Cave.Collections.Generic;
using Cave.Data;

namespace Cave.Web
{
    /// <summary>
    /// Provides a soap method
    /// </summary>
    public class WebServerMethod
    {
        /// <summary>Gets the page attribute of the specified method.</summary>
        /// <param name="method">The method.</param>
        /// <returns></returns>
        public static WebPageAttribute GetPageAttribute(MethodInfo method)
        {
            foreach (Attribute a in method.GetCustomAttributes(false))
            {
                if (a is WebPageAttribute)
                {
                    return (WebPageAttribute)a;
                }
            }
            return null;
        }

        object Instance;

        /// <summary>Gets a value indicating whether this instance is an action.</summary>
        /// <value><c>true</c> if this instance is an action; otherwise, <c>false</c>.</value>
        public bool IsAction => Instance is Action<WebData>;

        /// <summary>Gets the fullpath.</summary>
        /// <value>The fullpath.</value>
        public IEnumerable<string> FullPaths { get; }

        /// <summary>The method</summary>
        public MethodInfo Method { get; }

        /// <summary>The page attribute</summary>
        public WebPageAttribute PageAttribute { get; }

        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public string LogSourceName => $"EmbeddedWebServerMethod {Instance.GetType().Name}.{Method.Name}";

        /// <summary>Initializes a new instance of the <see cref="WebServerMethod" /> class.</summary>
        /// <param name="instance">The instance.</param>
        /// <param name="method">The method.</param>
        /// <param name="path">The path.</param>
        /// <exception cref="Exception"></exception>
        public WebServerMethod(object instance, MethodInfo method, string path)
        {
            Instance = instance;
            Method = method;
            PageAttribute = GetPageAttribute(Method);

            if (PageAttribute == null)
            {
                throw new InvalidDataException($"Page attribute missing at method {Method.Name}!");
            }

            int count = Method.GetParameters().Count(p => p.ParameterType == typeof(WebData));
            if (count != 1)
            {
                throw new InvalidDataException(string.Format("Method {0} needs exact one parameter of type {1}!", Method, nameof(WebData)));
            }

            Set<string> fullPaths = new Set<string>();
            foreach (string sub in PageAttribute.GetPaths())
            {
                try { fullPaths.Add(FileSystem.Combine('/', path, sub)); }
                catch (Exception ex) { throw new InvalidDataException($"{sub} path cannot be added to method {Method.Name}", ex); }
            }
            if (fullPaths.Count == 0)
            {
                try { fullPaths.Add(FileSystem.Combine('/', path, method.Name)); }
                catch (Exception ex) { throw new InvalidDataException($"{path} path cannot be added to method {Method.Name}", ex); }
            }
            FullPaths = fullPaths;

            foreach (ParameterInfo p in method.GetParameters())
            {
                try
                {
                    if (!p.IsOptional)
                    {
                        continue;
                    }
#if NET45 || NET46 || NET47 || NETSTANDARD20
                    if (p.HasDefaultValue)
#elif NET20 || NET35 || NET40
#else
#error No code defined for the current framework or NETXX version define missing!
#endif
                    {
                        if (p.DefaultValue != null && Convert.ToInt64(p.DefaultValue) != 0)
                        {
                            Trace.TraceError("Method <red>{0}<default> Parameter <red>{1}<default> has a not null default value!", method.Name, p);
                        }
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Invalid default value at method {0} parameter {1}!", method.Name, p), ex);
                }
            }
        }

        /// <summary>Gets the parameters.</summary>
        /// <value>The parameters.</value>
        public ParameterInfo[] Parameters => Method.GetParameters();

        /// <summary>Gets the name.</summary>
        /// <value>The name.</value>
        public string Name => Method.Name;

        /// <summary>Invokes the method using the specified data.</summary>
        /// <param name="data">The data.</param>
        /// <exception cref="Cave.Web.WebServerException">
        /// Could not convert parameter {0} value {1} to type {2}
        /// or
        /// Function {0}\nParameter {1} is missing!
        /// </exception>
        public void Invoke(WebData data)
        {
            data.Server.OnCheckSession(data);
            //auth required ?
            if (PageAttribute.AuthType != WebServerAuthType.None)
            {
                if (!data.Session.IsAuthenticated())
                {
                    Trace.TraceInformation("{0} {1}: Error call to <red>{2}<default> requires a valid user account. Elapsed {3}", data.Request.SourceAddress, data.Session, data.Request.DecodedUrl, data.Elapsed.FormatTime());
                    data.Result.AddMessage(data.Method, WebError.AuthenticationRequired, $"The requested URL {data.Request.DecodedUrl} requires a valid user account.");
                    if (data.Method?.PageAttribute?.AuthType == WebServerAuthType.Basic)
                    {
                        data.Result.Headers["WWW-Authenticate"] = $"Basic realm=\"{AssemblyVersionInfo.Program.Company} - {AssemblyVersionInfo.Program.Product}\"";
                    }
                    return;
                }
                data.Server.OnCheckAccess(data);
            }

            if (data.Server.VerboseMode)
            {
                Trace.TraceInformation("Request {0} Invoke Method {1}", data.Request, data.Method);
            }

            if (Instance is Action<WebData> func)
            {
                func.Invoke(data);
                if (data.Server.VerboseMode)
                {
                    Trace.TraceInformation("{0} {1}: Completed call to <green>{2}<default>. Elapsed {3}", data.Request.SourceAddress, data.Session, Name, data.Elapsed.FormatTime());
                }

                return;
            }

            Set<string> usedParameters = new Set<string>(data.Request.Parameters.Keys);
            ArrayList parameters = new ArrayList();
            foreach (ParameterInfo p in Method.GetParameters())
            {
                if (p.ParameterType == typeof(WebData))
                {
                    parameters.Add(data);
                    continue;
                }
                if (data.Request.Parameters.ContainsKey(p.Name))
                {
                    string value = data.Request.Parameters[p.Name];
                    if (value.Trim().Length > 0)
                    {
                        try
                        {
                            parameters.Add(Fields.ConvertValue(p.ParameterType, value, CultureInfo.InvariantCulture));
                        }
                        catch (Exception ex)
                        {
                            throw new WebServerException(ex, WebError.InvalidParameters, 0, string.Format("Could not convert parameter {0} value {1} to type {2}", p.Name, value, p.ParameterType.Name));
                        }
                        continue;
                    }
                }
                if (data.Request.MultiPartFormData != null)
                {
                    if (data.Request.MultiPartFormData.TryGet(p.Name, out WebSinglePart part))
                    {
                        //binary ?
                        if (p.ParameterType == typeof(byte[]))
                        {
                            parameters.Add(part.Content);
                        }
                        else
                        {
                            //no convert from string
                            string value = Encoding.UTF8.GetString(part.Content);
                            parameters.Add(Fields.ConvertValue(p.ParameterType, value, CultureInfo.InvariantCulture));
                        }
                        continue;
                    }
                }
                if (p.IsOptional)
                {
                    try
                    {
#if NET_45
                        if (p.HasDefaultValue)
#endif
                        {
                            parameters.Add(p.DefaultValue);
                            continue;
                        }
                    }
                    catch { Trace.TraceError("Invalid default value at parameter {0}!", p); }
                    parameters.Add(null);
                    continue;
                }
                throw new WebServerException(WebError.InvalidParameters, 0, string.Format("Function {0}\nParameter {1} is missing!", this, p.Name));
            }

            if (!PageAttribute.AllowAnyParameters)
            {
                foreach (string name in Method.GetParameters().Select(p => p.Name))
                {
                    usedParameters.TryRemove(name);
                }
                if (usedParameters.Count > 0)
                {
                    throw new WebServerException(WebError.InvalidParameters, 0, string.Format("Function {0}\nParameter {1} is unknown!", this, usedParameters.First()));
                }
            }

            Method.Invoke(Instance, parameters.ToArray());
            if (data.Server.VerboseMode)
            {
                Trace.TraceInformation("{0} {1}: Completed call to <green>{2}<default>. Elapsed {3}", data.Request.SourceAddress, data.Session, Name, data.Elapsed.FormatTime());
            }
        }

        /// <summary>Returns a string for the parameter definition.</summary>
        /// <returns>Returns a string for the parameter definition.</returns>
        public string ParameterString()
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;
            foreach (ParameterInfo p in Method.GetParameters())
            {
                if (p.ParameterType == typeof(WebData))
                {
                    continue;
                }

                if (++i > 1)
                {
                    sb.Append(", ");
                }

                if (p.IsOptional)
                {
                    sb.Append("[");
                }
                //sb.Append(p.ParameterType.Name);
                //sb.Append(" ");
                sb.Append(p.Name);
                if (p.IsOptional)
                {
                    sb.Append("]");
                }
            }
            return sb.ToString();
        }

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Instance.GetType().Name);
            sb.Append('.');
            sb.Append(Method.Name);
            sb.Append('(');
            sb.Append(ParameterString());
            sb.Append(')');
            return sb.ToString();
        }
    }
}

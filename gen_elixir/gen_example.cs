using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Igor.Elixir.AST;
using Igor.Elixir.Model;
using Igor.Elixir.Render;
using Igor.Text;

namespace Igor.Elixir
{

    [CustomAttributes]
    public class HttpServerExampleGenerator : IElixirGenerator
    {
        public static readonly StringAttributeDescriptor HttpExampleAttribute = new StringAttributeDescriptor("http.example", IgorAttributeTargets.WebService | IgorAttributeTargets.WebResource);
        public static readonly StringAttributeDescriptor HttpIfAttribute = new StringAttributeDescriptor("http.if", IgorAttributeTargets.WebService | IgorAttributeTargets.WebResource);
        public static readonly StringAttributeDescriptor HttpHintAttribute = new StringAttributeDescriptor("http.hint", IgorAttributeTargets.WebResource);

        private class Variable
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Guard { get; set; }
            public string Annotation { get; set; }
        }

        private class Route {
            public string Uri { get; set; }
            public string Module { get; set; }
        }

        public void Generate(ExModel model, Module mod)
        {
// // Console.WriteLine($"### MODEL: {model}");
// // Console.WriteLine(Inspect(model, "  "));
// // Console.WriteLine($"### TYPES: {mod}");
// foreach (var t in mod.Types) {
//     Console.WriteLine($"{t.Module}.{t}");
//     // Console.WriteLine(Inspect(t, "  "));
// }
            foreach (var service in mod.WebServices)
            {
                if (service.webServerEnabled)
                {
                    GenerateServerExample(model, service);
                }
            }
        }

        private string Inspect(Object obj, string indent = "")
        {
            var result = new List<string>();
            var props = obj.GetType().GetProperties();
            foreach (var p in props) {
                result.Add($"{indent}{p.Name}: {p.GetValue(obj, null)}");
            }
            result.Sort(delegate(string x, string y) {
                return string.Compare(x, y, StringComparison.Ordinal);
            });
            return result.JoinStrings("\n");
        }

        private List<Route> GetHandledRoutes(WebServiceForm service)
        {
            var routes = service.Resources.GroupBy(
                r => r.Path.Select(s => s.IsStatic ? s.StaticValue : $"\u00fe{s.Var.Name}").Prepend("").JoinStrings("/"),
                r => ExName.Combine(service.Module.exName, service.exName, r.Attribute(ExAttributes.HttpHandler)),
                (uri, handlers) => new Route {Uri = uri, Module = handlers.First()}
            ).ToList();
            routes.Sort((x, y) => string.Compare(x.Uri, y.Uri, StringComparison.Ordinal));
            return routes.Select(
                r => new Route {Uri = r.Uri.Replace("\u00fe", ":"), Module = r.Module}
            ).ToList();
        }

        private void GenerateServerExample(ExModel model, WebServiceForm service)
        {
            // foreach (var ttt in service.Resources.Select(resource => {
            //     return resource.Responses.Where(response => response.Content != null).Select(response => {
            //         // // if (response.Content.Type is Igor.Elixir.AST.IType) {
            //         // if (response.Content.Type.TypeHost != null is Collection) {
            //         //     // Console.WriteLine(Inspect(response.Content.Type.TypeHost));
            //         //     Console.WriteLine(Inspect(response.Content.Type));
            //         // // } else {
            //         // //     Console.WriteLine(Inspect(response.Content.Type));
            //         // }
            //         Console.WriteLine(response.Content.exType);
            //         Console.WriteLine("");
            //         return true;
            //         // var module = response.Content.Type.TypeHost.GetType().GetProperties().Where(p => p.Name == "Module").First().GetValue(response.Content.Type.TypeHost, null);
            //         // var structName = response.Content.Type;//.exName;//.exName;
            //         // return new {
            //         //     Module = module,
            //         //     Struct = structName,
            //         // };
            //     });
            // })) {
            //     // Console.WriteLine($"{ttt.Module}.{ttt.Struct}");
            //     foreach (var t in ttt) {
            //         // Console.WriteLine(Inspect(t));
            //     }
            // }

            service.Resources.Select((resource, index) =>
            {
                var callback = resource.exHttpServerCallback;
                var defaultExampleFileName = callback.Replace('.', '_').Format(Notation.LowerUnderscore) + ".ex.example";
                var exampleFileName = service.Attribute(HttpExampleAttribute, defaultExampleFileName);
                var ex = model.File(exampleFileName).Module(callback);
                ex.Behaviour(service.exHttpBehaviour);
                if (!string.IsNullOrEmpty(service.Annotation)) {
                    ex.Annotation = service.Annotation;
                }

                var callVars = new List<Variable>();
                var resultVars = new List<Variable>();
                var accessConditionVars = new List<Variable>();

                if (resource.RequestContent != null) {
                    callVars.Add(new Variable {
                        Name = resource.RequestContent.exRequestName,
                        Type = resource.RequestContent.exType,
                        Guard = Helper.ExGuard(resource.RequestContent.Type, resource.RequestContent.exRequestName),
                        Annotation = resource.RequestContent.Annotation,
                    });
                }

                foreach (var httpVar in resource.RequestVariables) {
                    callVars.Add(new Variable {
                        Name = httpVar.exName,
                        Type = httpVar.exType,
                        Annotation = httpVar.Annotation,
                        Guard = Helper.ExGuard(httpVar.Type, httpVar.exName)
                    });
                    foreach (var require in Helper.GuardRequires(httpVar.Type)) {
                        ex.Require(require);
                    }
                }

                var okResponse = resource.Responses.First();
                foreach (var httpVar in okResponse.HeadersVariables) {
                    resultVars.Add(new Variable {
                        Name = httpVar.exName,
                        Type = httpVar.exType
                    });
                }

                if (okResponse.Content != null) {
                    resultVars.Add(new Variable {
                        Name = okResponse.Content.exVarName("response_content"),
                        Type = okResponse.Content.exType
                    });
                }

                if (resource.exHttpSessionKey != null) {
                    callVars.Add(new Variable {
                        Name = "session",
                        Type = "any()",
                        // Annotation = string.Format("Session ({0})", resource.exHttpSessionKey)
                    });
                } else if (resource.exHttpSession) {
                    callVars.Add(new Variable {
                        Name = "session",
                        Type = "%{optional(String.t()) => any()}",
                        // Annotation = "Session map"
                    });
                }

                if (resource.exConn) {
                    callVars.Add(new Variable {
                        Name = "conn",
                        Type = "Plug.Conn.t()",
                        // Annotation = "Plug connection"
                    });
                    resultVars.Add(new Variable {
                        Name = "conn",
                        Type = "Plug.Conn.t()"
                    });
                }

                string resultType = "";
                if (resultVars.Count == 1) {
                    resultType = resultVars[0].Type;
                } else if (resultVars.Count > 1) {
                    resultType = resultVars.Select(v => v.Type).JoinStrings(", ").Quoted("{", "}");
                } else {
                    resultType = "any";
                }

                // write module header
                if (index == 0) {
                    ex.Block("# ----------------------------------------------------------------------------\n");
                    ex.Block("defmacro __using__(which) when is_atom(which), do: apply(__MODULE__, which, [])");
                }

                // expose routes via "use ..., :router"
                if (index == 0) {
                    var routes = GetHandledRoutes(service);
                    ex.Block("def router() do\n  quote do\n" + routes.Select(route => $"    match \"{route.Uri}\", to: {route.Module}").JoinStrings("\n") + "\n  end\nend");
                }

                ex.Block("# ----------------------------------------------------------------------------\n");

                var r = ExRenderer.Create();

                if (callVars.Any()) {
                    r += $"@spec {resource.exName}(";
                    r ++;
                    r.Blocks(callVars.Select(v => v.Name + " :: " + v.Type), delimiter: ",");
                    r --;
                    r += $") :: {resultType}";
                } else {
                    r += $"@spec {resource.exName}() :: {resultType}";
                }

                r += @"@impl true";
                if (callVars.Any()) {
                    r += $"def {resource.exName}(";
                    r ++;
                    var rows = callVars.Select(v => new[] { v.Name, string.IsNullOrEmpty(v.Annotation) ? "" : "# " + v.Annotation });
                    r.Table(rows, rowDelimiter: ",", delimiterPosition: 0);
                    r --;
                    if (callVars.Any(v => v.Guard != null)) {
                        r += ") when";
                        r ++;
                        r.Blocks(callVars.Where(v => v.Guard != null).Select(v => v.Guard), delimiter: " and");
                        r --;
                    } else {
                        r += ")";
                    }
                } else {
                    r += $"def {resource.exName}()";
                }

                r += "do";
                r ++;

                // write http.if forbidden guard
                var accessConditionAttribute = resource.Attribute(HttpIfAttribute);
                if (!string.IsNullOrEmpty(accessConditionAttribute)) {
                    var conditionVarName = (resource.exHttpSessionKey != null || resource.exHttpSession) ? "session" : "api_key";
                    r += $"unless {accessConditionAttribute}({conditionVarName}), do: raise DataProtocol.ForbiddenError";
                }

                // write standard crud
                var hintAttribute = resource.Attribute(HttpHintAttribute);
                if (!string.IsNullOrEmpty(hintAttribute)) {
                    // NB: we take context name from the first component of the service name
                    var contextModuleName = service.Name.Format(Notation.LowerUnderscore).Split('_').First().Format(Notation.UpperCamel);
                    var delegateVars = callVars
                        .Select(v => v.Name)
                        .Where(v => v != "api_key")
                    ;
                    if (resource.exHttpSessionKey != null || resource.exHttpSession) {
                        delegateVars = delegateVars
                            .Where(v => v != "session")
                        ;
                    }
                    if (resource.RequestContent != null) {
                        delegateVars = delegateVars
                            .Where(v => v != resource.RequestContent.exRequestName)
                            .Append(resource.RequestContent.exRequestName)
                        ;
                    }
                    var delegateArguments = delegateVars.JoinStrings(", ");

                    string resultExType = okResponse.Content.exType;
                    string resultStructName = resultExType.Replace(".t(", "-").Split('-')[0];
                    if (hintAttribute == "list") {
                        if (resultExType.Contains(".CollectionSlice.t(")) {
                            r += $"items = {contextModuleName}.{resource.exName}({delegateArguments})";
                            r += $"total = {contextModuleName}.{resource.exName.Replace("get_", "count_")}({delegateArguments})";
                            r += $"%{resultStructName}{{items: items, total: total}}";
                        } else if (resultExType.Contains(".Collection.t(")) {
                            r += $"items = {contextModuleName}.{resource.exName}({delegateArguments})";
                            r += $"%{resultStructName}{{items: items}}";
                        } else {
                            r += "raise \"not_yet_implemented\"";
                        }
                    } else if (hintAttribute == "read") {
                        r += $"item = {contextModuleName}.{resource.exName}!({delegateArguments})";
                        r += $"# log_user_action(session, :read, item)";
                        r += $"item";
                    } else if (hintAttribute == "create") {
                        r += $"item = {contextModuleName}.{resource.exName}!(Map.from_struct({delegateArguments}))";
                        r += $"# log_user_action(session, :create, item)";
                        r += $"item";
                    } else if (hintAttribute == "update") {
                        r += $"item = {contextModuleName}.{resource.exName}!({delegateArguments})";
                        r += $"# log_user_action(session, :update, item)";
                        r += $"item";
                    } else if (hintAttribute == "delete") {
                        r += $"item = {contextModuleName}.{resource.exName.Replace("delete_", "get_")}!({delegateArguments})";
                        r += $":ok = {contextModuleName}.{resource.exName}!({delegateArguments})";
                        r += $"# log_user_action(session, :delete, item)";
                        r += $"%{resultStructName}{{result: true}}";
                    }
                } else {
                    r += "raise \"not_yet_implemented\"";
                }
                r --;
                r += "end";

                ex.Function(r.Build()).Annotation = resource.Annotation;

                return index;
            }).ToList();
        }
    }

}

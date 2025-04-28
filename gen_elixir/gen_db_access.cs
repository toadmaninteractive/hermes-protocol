using System;
using System.Collections.Generic;
using System.Linq;

using Igor.Elixir.AST;
using Igor.Elixir.Model;
using Igor.Elixir.Render;
using Igor.Text;

namespace Igor.Elixir
{

    [CustomAttributes]
    public class RepoAccessGenerator : IElixirGenerator
    {
        public static readonly StringAttributeDescriptor DbAppAttribute = new StringAttributeDescriptor("db.app", IgorAttributeTargets.Module);
        public static readonly StringAttributeDescriptor DbEntityAttribute = new StringAttributeDescriptor("db.entity", IgorAttributeTargets.Record);
        public static readonly StringAttributeDescriptor DbPreloadAttribute = new StringAttributeDescriptor("db.preload", IgorAttributeTargets.Record);
        public static readonly StringAttributeDescriptor DbTakeAttribute = new StringAttributeDescriptor("db.take", IgorAttributeTargets.Record);

        public void Generate(ExModel model, Module mod)
        {
            // TODO: get rid of
            if (mod.Name != "DbProtocol") return;

            var fileName = mod.Name.Replace('.', '_').Format(Notation.LowerUnderscore) + "_impl.ex";
            var ex = model.File(fileName).Module($"{mod.Name}.Impl");
            var r = ExRenderer.Create();

            string dbApp = mod.Attribute(DbAppAttribute);

            // define imports etc
            if (!string.IsNullOrEmpty(dbApp)) {
                r += $"alias {dbApp}.{{Repo}}";
            }

            foreach (var record in mod.Records)
            {
                // skip those not having entity defined
                string dbEntity = record.Attribute(DbEntityAttribute);
                if (string.IsNullOrEmpty(dbEntity)) continue;

                // collect preload
                var dbPreload = record.Attribute(DbPreloadAttribute);
                string preload = "";
                if (!string.IsNullOrEmpty(dbPreload)) {
                    preload = string.Join(", ",
                        dbPreload.Split(" ").Select(x =>
                            x.Contains(".")
                                ? ("{" + string.Join(", ", x.Split(".").Select(y => $":{y}")) + "}")
                                : $":{x}"
                        )
                    );
                }

                // prelude
                r.EmptyLine();
                r += @"# ----------------------------------------------------------------------------";
                r.EmptyLine();
                if (!string.IsNullOrEmpty(record.Annotation)) {
                    r += $"# {record.Annotation}";
                }

                // define list mapper
                string targetName = NotationHelper.Format(record.Name, Notation.LowerUnderscore);
                r += $"@spec to_{targetName}([%{dbEntity}{{}}]) :: [%{mod.Name}.{record.Name}{{}}]";
                r += $"def to_{targetName}([]), do: []";
                r += $"def to_{targetName}([%{dbEntity}{{}} | _] = list) do";
                r ++;
                r += @"list";
                r ++;
                if (!string.IsNullOrEmpty(dbPreload)) {
                    r += $"|> Repo.preload([{preload}])";
                }
                r += $"|> Enum.map(&to_{targetName}/1)";
                r --;
                r --;
                r += @"end";

                // define struct mapper
                r += $"@spec to_{targetName}(%{dbEntity}{{}}) :: %{mod.Name}.{record.Name}{{}}";
                r += $"def to_{targetName}(%{dbEntity}{{}} = rec) do";
                r ++;
                if (!string.IsNullOrEmpty(dbPreload)) {
                    r += $"rec = rec |> Repo.preload([{preload}])";
                }
                r += $"%{mod.Name}.{record.Name}{{";
                r ++;
                foreach (var field in record.Fields)
                {
                    var accessPath = $"rec.{field.Name}";
                    var dbTake = field.Attribute(DbTakeAttribute);
                    if (!string.IsNullOrEmpty(dbTake)) {
                        if (dbTake.StartsWith("&")) {
                            // accessPath = $"rec |> then({dbTake})";
                            accessPath = $"({dbTake}).(rec)";
                        } else if (dbTake.StartsWith("fn")) {
                            // accessPath = $"rec |> then({dbTake})";
                            accessPath = $"({dbTake}).(rec)";
                        } else {
                            string[] steps = dbTake.Split("?");
                            accessPath = string.Join(" && ", steps.Select((step, index) => "rec." + string.Join("", steps.Take(index + 1))));
                        }
                    }
                    if (field.HasDefault) {
                        accessPath = $"({accessPath}) || {field.Default}";
                    }
                    if (!string.IsNullOrEmpty(field.Annotation)) {
                        r += $"# {field.Annotation}";
                    }
                    r += $"{field.Name}: {accessPath},";
                }
                r --;
                r += @"}";
                r --;
                r += @"end";
            }

            // postlude
            r.EmptyLine();
            r += @"# ----------------------------------------------------------------------------";
            r += @"# internal functions";
            r += @"# ----------------------------------------------------------------------------";
            r.EmptyLine();

            // Console.WriteLine(r.Build());
            ex.Block(r.Build()); //.Annotation = model.Annotation;
        }
    }

}

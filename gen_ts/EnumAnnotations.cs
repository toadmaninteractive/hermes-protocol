using Igor.Text;
using Igor.TypeScript.AST;
using Igor.TypeScript.Model;

namespace Igor.TypeScript
{
    [CustomAttributes]
    public class EnumAnnotations : ITsGenerator
    {
        public static readonly BoolAttributeDescriptor EnumDescriptionsAttribute = new BoolAttributeDescriptor("enum_descriptions", IgorAttributeTargets.Enum, AttributeInheritance.Scope);

        public void Generate(TsModel model, Module mod)
        {
            foreach (var e in mod.Enums)
            {
                if (e.Attribute(EnumDescriptionsAttribute, false))
                {
                    var ns = model.FileOf(e).Namespace(e.tsName);
                    ns.Function(string.Format(@"
export function getDescription(value: {0}): string {{
    switch (value) {{
{1}
        default: return '';
    }}
}}", e.tsName, e.Fields.JoinLines(CaseClause)));
                }
            }
        }

        private string CaseClause(EnumField field)
        {
            return string.Format("        case {0}.{1}: return '{2}';", field.Enum.tsName, field.tsName, field.Annotation);
        }
    }
}

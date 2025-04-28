using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Igor.TypeScript.AST;
using Igor.TypeScript.Model;
using Igor.Text;

namespace Igor.TypeScript
{
    public class NotificationGenerator : ITsGenerator
    {
        public static readonly BoolAttributeDescriptor NotificationAttribute = new BoolAttributeDescriptor("notification", IgorAttributeTargets.Variant);
        public static readonly StringAttributeDescriptor NotificationNameAttribute = new StringAttributeDescriptor("notification.name", IgorAttributeTargets.Variant);

        public void Generate(TsModel model, Module mod)
        {
            var variants = mod.Types.OfType<VariantForm>().Where(v => v.attributes.Attribute(NotificationAttribute, false)).ToList();
            foreach (var variant in variants)
            {
                var tsName = variant.attributes.Attribute(NotificationNameAttribute);
                var file = model.File(tsName.Format(Notation.LowerHyphen), tsName.Format(Notation.LowerHyphen) + ".service.ts");
                file.Import("import { Subject } from 'rxjs';");

                var cl = file.Class(tsName.Format(Notation.UpperCamel) + "Service");
                // cl.Decorator("@Injectable()");

                foreach (var rec in variant.Records)
                {
                    cl.Property(string.Format("{0} = new Subject<{1}>();", rec.tsName.Format(Notation.LowerCamel), rec.tsFullTypeName));
                }

                file.ImportModule(variant.Module.tsModule);

                var varName = variant.tsName.Format(Notation.LowerCamel);

                var r = new Renderer();
                r.Format("recv({0}: {1}): void {{", varName, variant.tsFullTypeName);
                r++;
                r.Format("switch ({0}.{1}) {{", varName, variant.TagField.tsName);
                r++;

                foreach (var rec in variant.Records)
                {
                    r.Format("case {0}.{1}:", rec.Module.tsName, rec.TagField.tsDefault);
                    r++;
                    r.Format("this.{0}.next(<{1}>{2});", rec.tsName.Format(Notation.LowerCamel), rec.tsFullTypeName, varName);
                    r += "break;";
                    r--;
                }

                r--;
                r += "}";
                r--;
                r += "}";

                cl.Function(r.Build());
            }
        }
    }
}

﻿namespace VSRAD.Syntax.Options
{
    public class FolderPathsEditor : System.Windows.Forms.Design.FolderNameEditor
    {
        public override object EditValue(System.ComponentModel.ITypeDescriptorContext context, System.IServiceProvider provider, object value)
        {
            var prevValue = value;
            var newValue = base.EditValue(context, provider, value);
            return (string.IsNullOrEmpty((string)prevValue) || newValue == prevValue) ? newValue : prevValue + ";" + newValue;
        }
    }
}

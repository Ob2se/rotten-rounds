using Sandbox;
using Editor;
using System;
using System.Linq;

namespace RRCustomContentLibrary.Editor;

[CustomEditor(typeof(WeaponOffsetValue))]
public class WeaponOffsetControl : ControlWidget
{
    public WeaponOffsetControl(SerializedProperty property) : base(property)
    {
        Layout = Layout.Column();
        
        // Standard Vector3 editor
        SerializedProperty valProp = null;
        if (property.TryGetAsObject(out var obj))
        {
            valProp = obj.GetProperty("Value");
        }

        if (valProp != null)
        {
            var vecControl = ControlWidget.Create(valProp);
            if (vecControl != null)
            {
                Layout.Add(vecControl);
            }
        }
        
        // Button
        var btn = new Button("Set Weapon Position");
        btn.Clicked = () => 
        {
            var window = new WeaponOffsetWindow(property);
            window.Show();
        };
        Layout.Add(btn);
    }
}

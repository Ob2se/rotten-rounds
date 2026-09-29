using Sandbox;
using Editor;
using System;
using System.Linq;
using System.Collections.Generic;

namespace RRCustomContentLibrary.Editor;

public class WeaponOffsetWindow : WidgetWindow
{
    private SerializedProperty _offsetProperty;
    
    private GameObject _previewPlayer;
    private GameObject _previewWeapon;
    private LineEdit _pathEdit;

    public WeaponOffsetWindow(SerializedProperty property)
    {
        _offsetProperty = property;
        
        WindowTitle = "Weapon Offset Setup";
        Size = new Vector2(400, 300);
        
        BuildUI();
    }

    private void BuildUI()
    {
        Layout = Layout.Column();
        Layout.Margin = 16;
        Layout.Spacing = 8;
        
        Layout.Add(new Label("1. Enter Character Prefab Path (must have 'thirdpersonweapon'):"));
        _pathEdit = new LineEdit("prefabs/player.prefab");
        Layout.Add(_pathEdit);

        var spawnBtn = new Button("2. Spawn Preview");
        spawnBtn.Clicked = SpawnPreview;
        Layout.Add(spawnBtn);

        Layout.AddStretchCell();

        var helpLbl = new Label("Use the standard Editor Move tools on the\n'Weapon Dummy' object, then click Save.");
        Layout.Add(helpLbl);

        var saveBtn = new Button("3. Save & Finish", "save");
        saveBtn.Clicked = SaveAndFinish;
        Layout.Add(saveBtn);

        var cancelBtn = new Button("Cancel");
        cancelBtn.Clicked = Close;
        Layout.Add(cancelBtn);
    }

    private void SpawnPreview()
    {
        CleanupPreview();

        var prefabPath = _pathEdit.Text;
        if (string.IsNullOrEmpty(prefabPath)) return;

        var prefab = ResourceLibrary.Get<PrefabFile>(prefabPath);
        if (prefab == null)
        {
            Log.Warning($"Weapon Offset: Prefab not found at {prefabPath}");
            return;
        }

        try
        {
            _previewPlayer = GameObject.Clone(prefabPath);
            _previewPlayer.Name = "Weapon Offset Preview (Temp)";

            var tpw = FindThirdPersonWeapon(_previewPlayer);
            if (tpw == null)
            {
                Log.Warning("Weapon Offset: Could not find 'thirdpersonweapon' GameObject. Attaching to root.");
                tpw = _previewPlayer;
            }

            _previewWeapon = new GameObject(true, "Weapon Dummy");
            _previewWeapon.SetParent(tpw, false);

            BaseWeapon baseWeapon = null;
            if (_offsetProperty.Parent != null && _offsetProperty.Parent.Targets != null)
            {
                foreach(var target in _offsetProperty.Parent.Targets)
                {
                    if (target is BaseWeapon bw)
                    {
                        baseWeapon = bw;
                        break;
                    }
                }
            }

            var smr = _previewWeapon.Components.Create<SkinnedModelRenderer>();
            if (baseWeapon != null && baseWeapon.WeaponModel != null && baseWeapon.WeaponModel.Model != null)
            {
                smr.Model = baseWeapon.WeaponModel.Model;
                smr.MaterialOverride = baseWeapon.WeaponModel.MaterialOverride;
            }
            else
            {
                Log.Info("Weapon Offset: Target BaseWeapon did not have a SkinnedModelRenderer assigned.");
            }

            _previewWeapon.LocalPosition = _offsetProperty.GetValue<WeaponOffsetValue>().Value;

            // Omitted automatic selection for API compatibility; user can select it from the Scene Outliner.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Weapon Offset: Failed to spawn preview.");
        }
    }

    private GameObject FindThirdPersonWeapon(GameObject root)
    {
        if (root.Name.Equals("thirdpersonweapon", StringComparison.OrdinalIgnoreCase)) return root;
        foreach (var child in root.Children)
        {
            var found = FindThirdPersonWeapon(child);
            if (found != null) return found;
        }
        return null;
    }

    private void SaveAndFinish()
    {
        if (_previewWeapon.IsValid())
        {
            // Update the serialized property
            _offsetProperty.SetValue(new WeaponOffsetValue { Value = _previewWeapon.LocalPosition });
            Log.Info($"Weapon Offset set to: {_previewWeapon.LocalPosition}");
        }

        CleanupPreview();
        Close();
    }

    public override void OnDestroyed()
    {
        CleanupPreview();
        base.OnDestroyed();
    }

    private void CleanupPreview()
    {
        if (_previewWeapon.IsValid())
        {
            _previewWeapon.Destroy();
        }
        if (_previewPlayer.IsValid())
        {
            _previewPlayer.Destroy();
        }
    }
}

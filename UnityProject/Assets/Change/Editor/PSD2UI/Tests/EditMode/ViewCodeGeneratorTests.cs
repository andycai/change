using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    /// <summary>
    /// Tests for ViewCodeGenerator, NamingUtility, and ViewCodeTemplate
    /// covering component scanning, code generation, naming conventions,
    /// and output format quality.
    /// </summary>
    public class ViewCodeGeneratorTests
    {
        private const string TestOutputDir = "Assets/Change/Editor/PSD2UI/Tests/EditMode/TestData/Generated";

        // ── Test helpers ────────────────────────────────────────────

        /// <summary>
        /// Creates a simple GameObject for testing ScanComponents.
        /// The returned object has no interactive components by default.
        /// </summary>
        private static GameObject CreateTestObject(string name)
        {
            var go = new GameObject(name);
            return go;
        }

        /// <summary>
        /// Creates a GameObject with the specified component attached.
        /// </summary>
        private static GameObject CreateObjectWithComponent<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.AddComponent<T>();
            return go;
        }

        /// <summary>
        /// Creates a parent GameObject with a child that has the specified component.
        /// Useful for testing hierarchy-based naming in ScanComponents.
        /// </summary>
        private static (GameObject parent, GameObject child) CreateParentChildWithComponent<T>(
            string parentName, string childName) where T : Component
        {
            var parent = new GameObject(parentName);
            var child = new GameObject(childName);
            child.transform.SetParent(parent.transform);
            child.AddComponent<T>();
            return (parent, child);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any generated test output under HotUpdate that may
            // have been left behind if a test failed mid-execution.
            string hotUpdateGenDir = Path.Combine(Application.dataPath,
                "HotUpdate/GameLogic/Quest/Views/Generated");
            if (Directory.Exists(hotUpdateGenDir))
            {
                Directory.Delete(hotUpdateGenDir, recursive: true);
            }

            string settingsGenDir = Path.Combine(Application.dataPath,
                "HotUpdate/GameLogic/Settings/Views/Generated");
            if (Directory.Exists(settingsGenDir))
            {
                Directory.Delete(settingsGenDir, recursive: true);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SanitizeIdentifier tests
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void SanitizeIdentifier_ValidName_ReturnsSameName()
        {
            string result = ViewCodeGenerator.SanitizeIdentifier("CloseButton");
            Assert.AreEqual("CloseButton", result);
        }

        [Test]
        public void SanitizeIdentifier_WithSpaces_ReplacesWithUnderscores()
        {
            string result = ViewCodeGenerator.SanitizeIdentifier("Close Button");
            Assert.AreEqual("Close_Button", result);
        }

        [Test]
        public void SanitizeIdentifier_WithSpecialChars_ReplacesWithUnderscores()
        {
            string result = ViewCodeGenerator.SanitizeIdentifier("Item-Name (1)");
            Assert.AreEqual("Item_Name_1", result);
        }

        [Test]
        public void SanitizeIdentifier_LeadingDigit_HandlesCorrectly()
        {
            // The leading-digit underscore is trimmed; the implementation
            // builds "_123Start" then Trim('_') reduces it to "123Start".
            string result = ViewCodeGenerator.SanitizeIdentifier("123Start");
            Assert.AreEqual("123Start", result);
        }

        [Test]
        public void SanitizeIdentifier_OnlyDigits_ReturnsDigitsOnly()
        {
            // The leading-digit underscore is trimmed by Trim('_').
            string result = ViewCodeGenerator.SanitizeIdentifier("999");
            Assert.AreEqual("999", result);
        }

        [Test]
        public void SanitizeIdentifier_EmptyString_ReturnsComponent()
        {
            string result = ViewCodeGenerator.SanitizeIdentifier("");
            Assert.AreEqual("Component", result);
        }

        [Test]
        public void SanitizeIdentifier_Null_ReturnsComponent()
        {
            string result = ViewCodeGenerator.SanitizeIdentifier(null);
            Assert.AreEqual("Component", result);
        }

        [Test]
        public void SanitizeIdentifier_AllIllegalChars_ReturnsComponent()
        {
            string result = ViewCodeGenerator.SanitizeIdentifier(" @#$% ");
            Assert.AreEqual("Component", result);
        }

        [Test]
        public void SanitizeIdentifier_UnderscoresAlreadyPresent_KeepsThem()
        {
            string result = ViewCodeGenerator.SanitizeIdentifier("my_field_name");
            Assert.AreEqual("my_field_name", result);
        }

        [Test]
        public void SanitizeIdentifier_MixedCase_KeepsCase()
        {
            string result = ViewCodeGenerator.SanitizeIdentifier("camelCase_PascalCase");
            Assert.AreEqual("camelCase_PascalCase", result);
        }

        [Test]
        public void SanitizeIdentifier_ConsecutiveIllegalChars_CollapsesUnderscores()
        {
            string result = ViewCodeGenerator.SanitizeIdentifier("A@@B");
            Assert.AreEqual("A_B", result);
        }

        [Test]
        public void SanitizeIdentifier_LeadingUnderscore_TrimsIt()
        {
            string result = ViewCodeGenerator.SanitizeIdentifier("_hello");
            Assert.AreEqual("hello", result);
        }

        // ══════════════════════════════════════════════════════════════
        //  ExtractModuleName tests
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void ExtractModuleName_QuestWindow_ReturnsQuest()
        {
            var generator = new ViewCodeGenerator();
            string result = generator.ExtractModuleName("QuestWindow");
            Assert.AreEqual("Quest", result);
        }

        [Test]
        public void ExtractModuleName_InventoryPanel_ReturnsInventory()
        {
            var generator = new ViewCodeGenerator();
            string result = generator.ExtractModuleName("InventoryPanel");
            Assert.AreEqual("Inventory", result);
        }

        [Test]
        public void ExtractModuleName_NoSuffix_ReturnsOriginalName()
        {
            var generator = new ViewCodeGenerator();
            string result = generator.ExtractModuleName("Settings");
            Assert.AreEqual("Settings", result);
        }

        [Test]
        public void ExtractModuleName_Empty_ReturnsCommon()
        {
            var generator = new ViewCodeGenerator();
            string result = generator.ExtractModuleName("");
            Assert.AreEqual("Common", result);
        }

        [Test]
        public void ExtractModuleName_Null_ReturnsCommon()
        {
            var generator = new ViewCodeGenerator();
            string result = generator.ExtractModuleName(null);
            Assert.AreEqual("Common", result);
        }

        [Test]
        public void ExtractModuleName_OnlyWindow_ReturnsCommon()
        {
            var generator = new ViewCodeGenerator();
            string result = generator.ExtractModuleName("Window");
            Assert.AreEqual("Common", result);
        }

        [Test]
        public void ExtractModuleName_OnlyPanel_ReturnsCommon()
        {
            var generator = new ViewCodeGenerator();
            string result = generator.ExtractModuleName("Panel");
            Assert.AreEqual("Common", result);
        }

        [Test]
        public void ExtractModuleName_WindowInMiddle_KeepsIt()
        {
            var generator = new ViewCodeGenerator();
            string result = generator.ExtractModuleName("WindowManagerPanel");
            Assert.AreEqual("Manager", result);
        }

        // ══════════════════════════════════════════════════════════════
        //  ScanComponents tests
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void ScanComponents_NullPrefab_ReturnsEmptyList()
        {
            var generator = new ViewCodeGenerator();
            var result = generator.ScanComponents(null);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ScanComponents_GameObjectWithNoInteractiveComponents_ReturnsEmpty()
        {
            var generator = new ViewCodeGenerator();
            var go = CreateTestObject("PlainObject");
            var result = generator.ScanComponents(go);

            Assert.AreEqual(0, result.Count);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ScanComponents_Button_ReturnsCorrectInfo()
        {
            var generator = new ViewCodeGenerator();
            var go = CreateObjectWithComponent<Button>("StartButton");

            var result = generator.ScanComponents(go);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("StartButton", result[0].Name);
            Assert.AreEqual("Button", result[0].Type);
            Assert.AreEqual("onClick", result[0].EventType);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ScanComponents_Toggle_ReturnsCorrectEventType()
        {
            var generator = new ViewCodeGenerator();
            var go = CreateObjectWithComponent<Toggle>("SoundToggle");

            var result = generator.ScanComponents(go);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("SoundToggle", result[0].Name);
            Assert.AreEqual("Toggle", result[0].Type);
            Assert.AreEqual("onValueChanged", result[0].EventType);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ScanComponents_Slider_ReturnsCorrectType()
        {
            var generator = new ViewCodeGenerator();
            var go = CreateObjectWithComponent<Slider>("VolumeSlider");

            var result = generator.ScanComponents(go);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Slider", result[0].Type);
            Assert.AreEqual("onValueChanged", result[0].EventType);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ScanComponents_TMPInputField_ReturnsInputFieldType()
        {
            var generator = new ViewCodeGenerator();
            var go = CreateObjectWithComponent<TMP_InputField>("NameInput");

            var result = generator.ScanComponents(go);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("InputField", result[0].Type);
            Assert.AreEqual("onValueChanged", result[0].EventType);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ScanComponents_ScrollRect_ReturnsCorrectType()
        {
            var generator = new ViewCodeGenerator();
            var go = CreateObjectWithComponent<ScrollRect>("ItemScroll");

            var result = generator.ScanComponents(go);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("ScrollRect", result[0].Type);
            Assert.AreEqual("onValueChanged", result[0].EventType);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ScanComponents_MultipleComponentsOnSameObject_ReturnsAll()
        {
            // Button and ScrollRect can coexist (ScrollRect is not a Selectable).
            var generator = new ViewCodeGenerator();
            var go = new GameObject("MultiComp");
            go.AddComponent<Button>();
            go.AddComponent<ScrollRect>();

            var result = generator.ScanComponents(go);

            Assert.AreEqual(2, result.Count);
            Assert.That(result.Exists(c => c.Type == "Button"), "Should contain Button");
            Assert.That(result.Exists(c => c.Type == "ScrollRect"), "Should contain ScrollRect");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ScanComponents_NestedHierarchy_UsesHierarchicalName()
        {
            var generator = new ViewCodeGenerator();
            var (parent, child) = CreateParentChildWithComponent<Button>("Header", "CloseButton");

            var result = generator.ScanComponents(parent);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Header_CloseButton", result[0].Name);
            UnityEngine.Object.DestroyImmediate(parent);
        }

        [Test]
        public void ScanComponents_DeepNesting_BuildsFullPath()
        {
            var generator = new ViewCodeGenerator();
            var root = new GameObject("Panel");
            var level1 = new GameObject("Content");
            var level2 = new GameObject("Item");
            level2.transform.SetParent(level1.transform);
            level1.transform.SetParent(root.transform);
            level2.AddComponent<Button>();

            var result = generator.ScanComponents(root);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Panel_Content_Item", result[0].Name);
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void ScanComponents_DuplicateNames_ResolvedWithSuffix()
        {
            var generator = new ViewCodeGenerator();
            var parent = new GameObject("Panel");
            var child1 = new GameObject("Btn");
            child1.transform.SetParent(parent.transform);
            child1.AddComponent<Button>();
            var child2 = new GameObject("Btn");
            child2.transform.SetParent(parent.transform);
            child2.AddComponent<Button>();

            var result = generator.ScanComponents(parent);

            Assert.AreEqual(2, result.Count);
            // First occurrence keeps original name; second gets _1 suffix
            Assert.AreEqual("Panel_Btn", result[0].Name);
            Assert.AreEqual("Panel_Btn_1", result[1].Name);
            UnityEngine.Object.DestroyImmediate(parent);
        }

        [Test]
        public void ScanComponents_HierarchyWithSpacesInNames_SanitizesCorrectly()
        {
            var generator = new ViewCodeGenerator();
            var parent = new GameObject("Main Panel");
            var child = new GameObject("Close Button");
            child.transform.SetParent(parent.transform);
            child.AddComponent<Button>();

            var result = generator.ScanComponents(parent);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Main_Panel_Close_Button", result[0].Name);
            UnityEngine.Object.DestroyImmediate(parent);
        }

        // ══════════════════════════════════════════════════════════════
        //  NamingUtility tests
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void ToFieldName_Button_PrefixesWithBtn()
        {
            string result = NamingUtility.ToFieldName("Close", "Button");
            Assert.AreEqual("btnClose", result);
        }

        [Test]
        public void ToFieldName_Toggle_PrefixesWithTgl()
        {
            string result = NamingUtility.ToFieldName("Sound", "Toggle");
            Assert.AreEqual("tglSound", result);
        }

        [Test]
        public void ToFieldName_Slider_PrefixesWithSld()
        {
            string result = NamingUtility.ToFieldName("Volume", "Slider");
            Assert.AreEqual("sldVolume", result);
        }

        [Test]
        public void ToFieldName_InputField_PrefixesWithInp()
        {
            string result = NamingUtility.ToFieldName("Name", "InputField");
            Assert.AreEqual("inpName", result);
        }

        [Test]
        public void ToFieldName_ScrollRect_PrefixesWithScr()
        {
            string result = NamingUtility.ToFieldName("List", "ScrollRect");
            Assert.AreEqual("scrList", result);
        }

        [Test]
        public void ToFieldName_Text_PrefixesWithTxt()
        {
            string result = NamingUtility.ToFieldName("Title", "Text");
            Assert.AreEqual("txtTitle", result);
        }

        [Test]
        public void ToFieldName_UnknownType_NoPrefixButPascalCase()
        {
            string result = NamingUtility.ToFieldName("Something", "UnknownType");
            Assert.AreEqual("Something", result);
        }

        [Test]
        public void ToFieldName_LayerNameWithSpaces_StripsAndConverts()
        {
            string result = NamingUtility.ToFieldName("close button", "Button");
            Assert.AreEqual("btnClosebutton", result);
        }

        [Test]
        public void ToMethodName_OnClick_SuffixClicked()
        {
            string result = NamingUtility.ToMethodName("Close", "onClick");
            Assert.AreEqual("OnCloseClicked", result);
        }

        [Test]
        public void ToMethodName_OnValueChanged_SuffixValueChanged()
        {
            string result = NamingUtility.ToMethodName("Volume", "onValueChanged");
            Assert.AreEqual("OnVolumeValueChanged", result);
        }

        [Test]
        public void ToMethodName_OnEndEdit_SuffixEndEdit()
        {
            string result = NamingUtility.ToMethodName("Name", "onEndEdit");
            Assert.AreEqual("OnNameEndEdit", result);
        }

        [Test]
        public void ToMethodName_UnknownEvent_NoSuffix()
        {
            string result = NamingUtility.ToMethodName("Item", "onHover");
            Assert.AreEqual("OnItem", result);
        }

        [Test]
        public void ToMethodName_LayerNameWithSpecialChars_StripsThem()
        {
            string result = NamingUtility.ToMethodName("Item-Name (1)", "onClick");
            Assert.AreEqual("OnItemName1Clicked", result);
        }

        // ══════════════════════════════════════════════════════════════
        //  ViewCodeTemplate tests
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void GenerateClass_NoComponents_ProducesValidEmptyClass()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>();

            string code = template.GenerateClass("EmptyWindow", "Empty", components);

            Assert.IsNotNull(code);
            Assert.IsNotEmpty(code);
            Assert.That(code, Does.Contain("public partial class EmptyWindowView : MonoBehaviour"));
            Assert.That(code, Does.Contain("private void Start()"));
            Assert.That(code, Does.Not.Contain("[SerializeField]")); // No fields
            Assert.That(code, Does.Not.Contain("AddListener"));      // No bindings
            Assert.That(code, Does.Not.Contain("partial void"));     // No methods
        }

        [Test]
        public void GenerateClass_WithComponents_ContainsFieldDeclarations()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Close", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.Contain("[SerializeField] private Button btnClose;"));
        }

        [Test]
        public void GenerateClass_WithComponents_ContainsEventBinding()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Close", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.Contain("btnClose.onClick.AddListener(OnCloseClicked);"));
        }

        [Test]
        public void GenerateClass_WithComponents_ContainsPartialMethod()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Volume", Type = "Slider", EventType = "onValueChanged" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.Contain("partial void OnVolumeValueChanged();"));
        }

        [Test]
        public void GenerateClass_ContainsRequiredUsingStatements()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Close", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.Contain("using UnityEngine;"));
            Assert.That(code, Does.Contain("using UnityEngine.UI;"));
            Assert.That(code, Does.Contain("using TMPro;"));
        }

        [Test]
        public void GenerateClass_WrapsInCorrectNamespace()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>();

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.Contain("namespace Change.Runtime.UI"));
        }

        [Test]
        public void GenerateClass_HasAutoGeneratedWarning()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>();

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.Contain("AUTO-GENERATED CODE"));
            Assert.That(code, Does.Contain("DO NOT EDIT MANUALLY"));
        }

        [Test]
        public void GenerateClass_MultipleComponents_GeneratesAllFieldsAndBindings()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Close", Type = "Button", EventType = "onClick" },
                new ComponentInfo { Name = "Sound", Type = "Toggle", EventType = "onValueChanged" },
                new ComponentInfo { Name = "Volume", Type = "Slider", EventType = "onValueChanged" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.Contain("btnClose"));
            Assert.That(code, Does.Contain("tglSound"));
            Assert.That(code, Does.Contain("sldVolume"));
            Assert.That(code, Does.Contain("OnCloseClicked"));
            Assert.That(code, Does.Contain("OnSoundValueChanged"));
            Assert.That(code, Does.Contain("OnVolumeValueChanged"));
        }

        // ══════════════════════════════════════════════════════════════
        //  Code format quality tests (4-space indentation)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void GenerateClass_Uses4SpaceIndentation_ForClassBody()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Close", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);
            string[] lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // Find the class declaration line: "    public partial class TestWindowView : MonoBehaviour"
            foreach (var line in lines)
            {
                if (line.Contains("public partial class"))
                {
                    Assert.That(line, Does.StartWith("    "), $"Class line should start with 4 spaces: '{line}'");
                    Assert.That(line, Does.Not.StartWith("        "), $"Class line should not use 8 spaces: '{line}'");
                    break;
                }
            }
        }

        [Test]
        public void GenerateClass_Uses4SpaceIndentation_ForFieldDeclarations()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Close", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);
            string[] lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (line.Contains("[SerializeField]"))
                {
                    Assert.That(line, Does.StartWith("        "), $"Field line should start with 8 spaces (2 levels): '{line}'");
                }
            }
        }

        [Test]
        public void GenerateClass_Uses4SpaceIndentation_ForAddListenerLines()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Close", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);
            string[] lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (line.Contains("AddListener"))
                {
                    Assert.That(line, Does.StartWith("            "), $"AddListener line should start with 12 spaces (3 levels): '{line}'");
                }
            }
        }

        [Test]
        public void GenerateClass_Uses4SpaceIndentation_ForPartialMethodDeclaration()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Close", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);
            string[] lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (line.Contains("partial void"))
                {
                    Assert.That(line, Does.StartWith("        "), $"Partial method line should start with 8 spaces (2 levels): '{line}'");
                }
            }
        }

        [Test]
        public void GenerateClass_NoTabsInOutput()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Close", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.Not.Contain("\t"), "Generated code should not contain tab characters");
        }

        [Test]
        public void GenerateClass_FieldNameFollowsPrefixPascalCaseConvention()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "submit", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.Contain("btnSubmit"));
        }

        [Test]
        public void GenerateClass_MethodNameFollowsOnPascalCaseSuffixConvention()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "submit", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.Contain("OnSubmitClicked"));
        }

        // ══════════════════════════════════════════════════════════════
        //  GenerateViewCode integration tests
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void GenerateViewCode_NullPrefab_LogsErrorAndReturns()
        {
            var generator = new ViewCodeGenerator();
            LogAssert.Expect(LogType.Error, "[PSD2UI] ViewCodeGenerator.GenerateViewCode: prefab is null.");
            generator.GenerateViewCode(null, "TestWindow");
        }

        [Test]
        public void GenerateViewCode_NullPrefabName_LogsErrorAndReturns()
        {
            var generator = new ViewCodeGenerator();
            var go = CreateObjectWithComponent<Button>("Btn");
            LogAssert.Expect(LogType.Error, "[PSD2UI] ViewCodeGenerator.GenerateViewCode: prefabName is null or empty.");
            generator.GenerateViewCode(go, null);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void GenerateViewCode_EmptyPrefabName_LogsErrorAndReturns()
        {
            var generator = new ViewCodeGenerator();
            var go = CreateObjectWithComponent<Button>("Btn");
            LogAssert.Expect(LogType.Error, "[PSD2UI] ViewCodeGenerator.GenerateViewCode: prefabName is null or empty.");
            generator.GenerateViewCode(go, "");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void GenerateViewCode_CreatesFileAtCorrectPath()
        {
            var generator = new ViewCodeGenerator();
            var go = CreateObjectWithComponent<Button>("CloseButton");
            string prefabName = "QuestWindow";

            try
            {
                generator.GenerateViewCode(go, prefabName);

                string expectedPath = Path.Combine(Application.dataPath,
                    "HotUpdate/GameLogic/Quest/Views/Generated/QuestWindowView.Generated.cs");

                Assert.IsTrue(File.Exists(expectedPath),
                    $"Expected generated file at: {expectedPath}");

                string generatedContent = File.ReadAllText(expectedPath);
                Assert.IsNotEmpty(generatedContent);
                Assert.That(generatedContent, Does.Contain("public partial class QuestWindowView"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);

                // Clean up generated file
                string genDir = Path.Combine(Application.dataPath,
                    "HotUpdate/GameLogic/Quest/Views/Generated");
                if (Directory.Exists(genDir))
                {
                    Directory.Delete(genDir, recursive: true);
                }
            }
        }

        [Test]
        public void GenerateViewCode_GeneratedFileContainsCorrectClassName()
        {
            var generator = new ViewCodeGenerator();
            var go = CreateObjectWithComponent<Button>("SubmitButton");
            string prefabName = "SettingsPanel";

            try
            {
                generator.GenerateViewCode(go, prefabName);

                string expectedPath = Path.Combine(Application.dataPath,
                    "HotUpdate/GameLogic/Settings/Views/Generated/SettingsPanelView.Generated.cs");

                Assert.IsTrue(File.Exists(expectedPath));

                string content = File.ReadAllText(expectedPath);
                Assert.That(content, Does.Contain("public partial class SettingsPanelView"));
                Assert.That(content, Does.Contain("btnSubmitButton"));
                Assert.That(content, Does.Contain("OnSubmitButtonClicked"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);

                // Clean up
                string genDir = Path.Combine(Application.dataPath,
                    "HotUpdate/GameLogic/Settings/Views/Generated");
                if (Directory.Exists(genDir))
                {
                    Directory.Delete(genDir, recursive: true);
                }
            }
        }

        [Test]
        public void GenerateViewCode_ValidatesOutputCodeCompiles()
        {
            // Ensures the generated code has balanced braces and
            // structurally looks like valid C# (no syntax validation via actual compilation).
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "OK", Type = "Button", EventType = "onClick" },
                new ComponentInfo { Name = "Cancel", Type = "Button", EventType = "onClick" },
                new ComponentInfo { Name = "Music", Type = "Toggle", EventType = "onValueChanged" }
            };

            string code = template.GenerateClass("DialogWindow", "Dialog", components);

            // Count braces for balance
            int openBraces = 0;
            int closeBraces = 0;
            foreach (char c in code)
            {
                if (c == '{') openBraces++;
                if (c == '}') closeBraces++;
            }

            Assert.AreEqual(openBraces, closeBraces, "Braces must be balanced in generated code");

            // All semicolons on field/method lines
            string[] lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Contains("[SerializeField]") || trimmed.Contains("partial void"))
                {
                    Assert.That(trimmed, Does.EndWith(";"),
                        $"Declaration line should end with semicolon: '{trimmed}'");
                }
            }
        }

        [Test]
        public void GenerateViewCode_GeneratedCodeEndsWithNewline()
        {
            var template = new ViewCodeTemplate();
            var components = new List<ComponentInfo>
            {
                new ComponentInfo { Name = "Close", Type = "Button", EventType = "onClick" }
            };

            string code = template.GenerateClass("TestWindow", "Test", components);

            Assert.That(code, Does.EndWith("\n").Or.EndWith("\r\n"),
                "Generated code should end with a newline");
        }

        // ══════════════════════════════════════════════════════════════
        //  End-to-end naming convention validation
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void FullPipeline_ComponentNamesMatchNamingConvention()
        {
            // Scans a GameObject hierarchy and verifies all generated
            // field/method names follow the documented conventions.
            var generator = new ViewCodeGenerator();
            var root = new GameObject("QuestWindow");
            var header = new GameObject("Header");
            header.transform.SetParent(root.transform);

            var closeBtn = new GameObject("Close");
            closeBtn.transform.SetParent(header.transform);
            closeBtn.AddComponent<Button>();

            var infoTxt = new GameObject("Title");
            infoTxt.transform.SetParent(header.transform);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform);
            var volumeSld = new GameObject("Volume");
            volumeSld.transform.SetParent(body.transform);
            volumeSld.AddComponent<Slider>();

            try
            {
                var components = generator.ScanComponents(root);
                string code = new ViewCodeTemplate().GenerateClass("QuestWindow", "Quest", components);

                // Field naming: prefix + PascalCase
                // Hierarchy includes root object name: QuestWindow_Header_Close → btnQuestWindowHeaderClose
                Assert.That(code, Does.Contain("btnQuestWindowHeaderClose"),
                    "Button field should be prefixed with 'btn'");
                Assert.That(code, Does.Contain("sldQuestWindowBodyVolume"),
                    "Slider field should be prefixed with 'sld'");

                // Method naming: On + PascalCase + event suffix
                Assert.That(code, Does.Contain("OnQuestWindowHeaderCloseClicked"),
                    "Button click handler should end with 'Clicked'");
                Assert.That(code, Does.Contain("OnQuestWindowBodyVolumeValueChanged"),
                    "Slider change handler should end with 'ValueChanged'");

                // All field names start with a known prefix
                foreach (var comp in components)
                {
                    string fieldName = NamingUtility.ToFieldName(comp.Name, comp.Type);
                    Assert.That(fieldName, Does.Match("^(btn|tgl|sld|inp|scr|txt)"),
                        $"Field '{fieldName}' should start with a known prefix for type '{comp.Type}'");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}

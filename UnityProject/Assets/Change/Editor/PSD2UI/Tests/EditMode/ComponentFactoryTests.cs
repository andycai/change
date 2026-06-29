// UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/ComponentFactoryTests.cs

using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    /// <summary>
    /// EditMode tests for ComponentFactory.
    /// Tests both the original API (no info param) and the extended API (with ComponentInfo).
    /// </summary>
    public class ComponentFactoryTests
    {
        private ComponentFactory factory;
        private GameObject testGo;

        [SetUp]
        public void SetUp()
        {
            factory = new ComponentFactory();
            testGo = new GameObject("TestGO");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testGo);
        }

        // =====================================================================
        // 向后兼容：无 info 参数（原有 API）
        // =====================================================================

        [Test]
        public void CreateComponent_Image_NoInfo_CreatesImage()
        {
            var component = factory.CreateComponent("Image", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Image>(component);
        }

        [Test]
        public void CreateComponent_Text_NoInfo_CreatesTMP()
        {
            var component = factory.CreateComponent("Text", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<TextMeshProUGUI>(component);
        }

        [Test]
        public void CreateComponent_Button_NoInfo_CreatesButton()
        {
            var component = factory.CreateComponent("Button", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Button>(component);
        }

        [Test]
        public void CreateComponent_ScrollRect_NoInfo_CreatesScrollRect()
        {
            var component = factory.CreateComponent("ScrollRect", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<ScrollRect>(component);
        }

        [Test]
        public void CreateComponent_InputField_NoInfo_CreatesTMPInputField()
        {
            var component = factory.CreateComponent("InputField", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<TMP_InputField>(component);
        }

        // =====================================================================
        // 新组件类型
        // =====================================================================

        [Test]
        public void CreateComponent_RawImage_CreatesRawImage()
        {
            var component = factory.CreateComponent("RawImage", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<RawImage>(component);
        }

        [Test]
        public void CreateComponent_Dropdown_CreatesDropdown()
        {
            var component = factory.CreateComponent("Dropdown", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Dropdown>(component);
        }

        [Test]
        public void CreateComponent_Toggle_CreatesToggle()
        {
            var component = factory.CreateComponent("Toggle", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Toggle>(component);
        }

        [Test]
        public void CreateComponent_Slider_CreatesSlider()
        {
            var component = factory.CreateComponent("Slider", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Slider>(component);
        }

        [Test]
        public void CreateComponent_Mask_CreatesMask()
        {
            var component = factory.CreateComponent("Mask", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Mask>(component);
        }

        [Test]
        public void CreateComponent_FillColor_FallsBackToImage()
        {
            var component = factory.CreateComponent("FillColor", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Image>(component);
        }

        [Test]
        public void CreateComponent_ScrollView_CreatesScrollRect()
        {
            var component = factory.CreateComponent("ScrollView", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<ScrollRect>(component);
        }

        [Test]
        public void CreateComponent_VerticalLayoutGroup_CreatesVLG()
        {
            var component = factory.CreateComponent("VerticalLayoutGroup", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<VerticalLayoutGroup>(component);
        }

        [Test]
        public void CreateComponent_HorizontalLayoutGroup_CreatesHLG()
        {
            var component = factory.CreateComponent("HorizontalLayoutGroup", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<HorizontalLayoutGroup>(component);
        }

        [Test]
        public void CreateComponent_GridLayoutGroup_CreatesGLG()
        {
            var component = factory.CreateComponent("GridLayoutGroup", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<GridLayoutGroup>(component);
        }

        // =====================================================================
        // 扩展属性：ImageType
        // =====================================================================

        [Test]
        public void CreateComponent_Image_WithSlicedImageType_SetsImageTypeSliced()
        {
            var info = new ComponentInfo { Type = "Image", ImageType = "sliced" };
            var component = factory.CreateComponent("Image", testGo, info);

            Assert.IsNotNull(component);
            var image = testGo.GetComponent<Image>();
            Assert.IsNotNull(image);
            Assert.AreEqual(Image.Type.Sliced, image.type);
        }

        [Test]
        public void CreateComponent_Image_WithSimpleImageType_SetsImageTypeSimple()
        {
            var info = new ComponentInfo { Type = "Image", ImageType = "simple" };
            var component = factory.CreateComponent("Image", testGo, info);

            var image = testGo.GetComponent<Image>();
            Assert.AreEqual(Image.Type.Simple, image.type);
        }

        [Test]
        public void CreateComponent_Image_WithTiledImageType_SetsImageTypeTiled()
        {
            var info = new ComponentInfo { Type = "Image", ImageType = "tiled" };
            var component = factory.CreateComponent("Image", testGo, info);

            var image = testGo.GetComponent<Image>();
            Assert.AreEqual(Image.Type.Tiled, image.type);
        }

        [Test]
        public void CreateComponent_Image_WithFilledImageType_SetsImageTypeFilled()
        {
            var info = new ComponentInfo { Type = "Image", ImageType = "filled" };
            var component = factory.CreateComponent("Image", testGo, info);

            var image = testGo.GetComponent<Image>();
            Assert.AreEqual(Image.Type.Filled, image.type);
        }

        [Test]
        public void CreateComponent_Image_WithNullInfo_UsesDefaultImageType()
        {
            var component = factory.CreateComponent("Image", testGo, null);

            var image = testGo.GetComponent<Image>();
            Assert.IsNotNull(image);
            // Default Image.type is Simple
            Assert.AreEqual(Image.Type.Simple, image.type);
        }

        // =====================================================================
        // 扩展属性：TextBackend
        // =====================================================================

        [Test]
        public void CreateComponent_Text_WithTMPBackend_CreatesTMPComponent()
        {
            var info = new ComponentInfo { Type = "Text", TextBackend = "tmp" };
            var component = factory.CreateComponent("Text", testGo, info);

            Assert.IsNotNull(component);
            Assert.IsInstanceOf<TextMeshProUGUI>(component);
        }

        [Test]
        public void CreateComponent_Text_WithUGUIBackend_CreatesUGUIText()
        {
            var info = new ComponentInfo { Type = "Text", TextBackend = "ugui" };
            var component = factory.CreateComponent("Text", testGo, info);

            Assert.IsNotNull(component);
            Assert.IsInstanceOf<UnityEngine.UI.Text>(component);
        }

        [Test]
        public void CreateComponent_Text_WithNullBackend_DefaultsToTMP()
        {
            var info = new ComponentInfo { Type = "Text", TextBackend = null };
            var component = factory.CreateComponent("Text", testGo, info);

            Assert.IsNotNull(component);
            Assert.IsInstanceOf<TextMeshProUGUI>(component);
        }

        // =====================================================================
        // 边界情况
        // =====================================================================

        [Test]
        public void CreateComponent_NullTarget_ReturnsNull()
        {
            var component = factory.CreateComponent("Image", null);
            Assert.IsNull(component);
        }

        [Test]
        public void CreateComponent_NullType_ReturnsNull()
        {
            var component = factory.CreateComponent(null, testGo);
            Assert.IsNull(component);
        }

        [Test]
        public void CreateComponent_UnknownType_ReturnsNull()
        {
            var component = factory.CreateComponent("UnknownComponent", testGo);
            Assert.IsNull(component);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eto.Forms;

namespace glTF_BinExporter
{
    class ExportOptionsDialog : Dialog<DialogResult>
    {
        private const int DefaultPadding = 5;
        private static readonly Eto.Drawing.Size DefaultSpacing = new Eto.Drawing.Size(2, 2);

        private CheckBox useDracoCompressionCheck = new CheckBox();

        private Label dracoCompressionLabel = new Label();
        private NumericStepper dracoCompressionLevelInput = new NumericStepper();

        private Label dracoQuantizationBitsLabel = new Label();
        private NumericStepper dracoQuantizationBitsInputPosition = new NumericStepper() { DecimalPlaces = 0, MinValue = 8, MaxValue = 32 };
        private NumericStepper dracoQuantizationBitsInputNormal = new NumericStepper() { DecimalPlaces = 0, MinValue = 8, MaxValue = 32 };
        private NumericStepper dracoQuantizationBitsInputTexture = new NumericStepper() { DecimalPlaces = 0, MinValue = 8, MaxValue = 32 };
        
        private Button cancelButton = new Button();
        private Button okButton = new Button();

        private CheckBox mapZtoY = new CheckBox();
        private CheckBox exportMaterials = new CheckBox();
        private CheckBox useDisplayColorForUnsetMaterial = new CheckBox();

        public ExportOptionsDialog()
        {
            Resizable = false;

            Title = "glTF Export Options";

            useDracoCompressionCheck.Text = "Use Draco Compression";
            
            dracoCompressionLabel.Text = "Draco Compression Level";
            dracoCompressionLevelInput.DecimalPlaces = 0;
            dracoCompressionLevelInput.MinValue = 1;
            dracoCompressionLevelInput.MaxValue = 10;

            dracoQuantizationBitsLabel.Text = "Quantization";

            cancelButton.Text = "Cancel";

            okButton.Text = "Ok";

            mapZtoY.Text = "Map Rhino Z to glTF Y";

            exportMaterials.Text = "Export Materials";
            useDisplayColorForUnsetMaterial.Text = "Use display color for objects with no material set";

            OptionsToDialog();

            useDracoCompressionCheck.CheckedChanged += UseDracoCompressionCheck_CheckedChanged;
            exportMaterials.CheckedChanged += ExportMaterials_CheckedChanged;

            cancelButton.Click += CancelButton_Click;
            okButton.Click += OkButton_Click;

            var gBox = new GroupBox() { Text = "Draco Quantization Bits" };
            gBox.Content = new TableLayout()
            {
                Padding = DefaultPadding,
                Spacing = DefaultSpacing,
                Rows = {
                    new TableRow("Position", "Normal", "Texture"),
                    new TableRow(dracoQuantizationBitsInputPosition, dracoQuantizationBitsInputNormal, dracoQuantizationBitsInputTexture)
                }
            };

            var layout = new DynamicLayout()
            {
                Padding = DefaultPadding,
                Spacing = DefaultSpacing,
            };

            layout.AddSeparateRow(useDracoCompressionCheck, null);
            layout.AddSeparateRow(dracoCompressionLabel, dracoCompressionLevelInput, null);
            layout.AddSeparateRow(gBox, null);

            TabControl tabControl = new TabControl();

            TabPage formattingPage = new TabPage()
            {
                Text = "Formatting",
                Content = new TableLayout()
                {
                    Padding = DefaultPadding,
                    Spacing = DefaultSpacing,
                    Rows =
                    {
                        new TableRow(mapZtoY),
                        new TableRow(exportMaterials),
                        new TableRow(useDisplayColorForUnsetMaterial),
                    },
                },
            };

            TabPage compressionPage = new TabPage()
            {
                Text = "Compression",
                Content = layout,
            };

            tabControl.Pages.Add(formattingPage);
            tabControl.Pages.Add(compressionPage);

            this.Content = new TableLayout()
            {
                Padding = DefaultPadding,
                Spacing = DefaultSpacing,
                Rows =
                {
                    new TableRow(tabControl),
                    null,
                    new TableRow(new TableLayout()
                    {
                        Padding = DefaultPadding,
                        Spacing = DefaultSpacing,
                        Rows =
                        {
                            new TableRow(cancelButton, okButton),
                        }
                    }),
                }
            };
        }

        private void OptionsToDialog()
        {
            mapZtoY.Checked = glTFBinExporterPlugin.MapRhinoZToGltfY;
            exportMaterials.Checked = glTFBinExporterPlugin.ExportMaterials;
            EnableDisableMaterialControls(glTFBinExporterPlugin.ExportMaterials);

            useDisplayColorForUnsetMaterial.Checked = glTFBinExporterPlugin.UseDisplayColorForUnsetMaterials;

            useDracoCompressionCheck.Checked = glTFBinExporterPlugin.UseDracoCompression;
            EnableDisableDracoControls(glTFBinExporterPlugin.UseDracoCompression);

            dracoCompressionLevelInput.Value = glTFBinExporterPlugin.DracoCompressionLevel;
            dracoQuantizationBitsInputPosition.Value = glTFBinExporterPlugin.DracoQuantizationBitsPosition;
            dracoQuantizationBitsInputNormal.Value = glTFBinExporterPlugin.DracoQuantizationBitsNormal;
            dracoQuantizationBitsInputTexture.Value = glTFBinExporterPlugin.DracoQuantizationBitsTexture;
        }

        private void DialogToOptions()
        {
            glTFBinExporterPlugin.MapRhinoZToGltfY = GetCheckboxValue(mapZtoY);
            glTFBinExporterPlugin.ExportMaterials = GetCheckboxValue(exportMaterials);
            glTFBinExporterPlugin.UseDisplayColorForUnsetMaterials = GetCheckboxValue(useDisplayColorForUnsetMaterial);

            glTFBinExporterPlugin.UseDracoCompression = GetCheckboxValue(useDracoCompressionCheck);
            glTFBinExporterPlugin.DracoCompressionLevel = (int)dracoCompressionLevelInput.Value;
            glTFBinExporterPlugin.DracoQuantizationBitsPosition = (int)dracoQuantizationBitsInputPosition.Value;
            glTFBinExporterPlugin.DracoQuantizationBitsNormal = (int)dracoQuantizationBitsInputNormal.Value;
            glTFBinExporterPlugin.DracoQuantizationBitsTexture = (int)dracoQuantizationBitsInputTexture.Value;
        }

        private bool GetCheckboxValue(CheckBox checkBox)
        {
            return checkBox.Checked.HasValue ? checkBox.Checked.Value : false;
        }

        private void EnableDisableDracoControls(bool enable)
        {
            dracoCompressionLevelInput.Enabled = enable;
            dracoQuantizationBitsInputPosition.Enabled = enable;
            dracoQuantizationBitsInputNormal.Enabled = enable;
            dracoQuantizationBitsInputTexture.Enabled = enable;
        }

        private void UseDracoCompressionCheck_CheckedChanged(object sender, EventArgs e)
        {
            bool useDraco = GetCheckboxValue(useDracoCompressionCheck);

            EnableDisableDracoControls(useDraco);
        }

        private void ExportMaterials_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = GetCheckboxValue(exportMaterials);

            EnableDisableMaterialControls(enabled);
        }

        private void EnableDisableMaterialControls(bool enabled)
        {
            useDisplayColorForUnsetMaterial.Enabled = enabled;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.Close(DialogResult.Cancel);
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            DialogToOptions();

            this.Close(DialogResult.Ok);
        }
    }
}

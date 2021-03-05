using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eto.Forms;

namespace glTF_BinExporter
{
    class ExportOptionsDialog : Dialog<glTFExportOptions>
    {
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

        private glTFExportOptions options = null;

        public ExportOptionsDialog(glTFExportOptions options)
        {
            this.options = options;

            useDracoCompressionCheck.Text = "Use Draco Compression";
            
            dracoCompressionLabel.Text = "Draco Compression Level";
            dracoCompressionLevelInput.DecimalPlaces = 0;
            dracoCompressionLevelInput.MinValue = 1;
            dracoCompressionLevelInput.MaxValue = 10;

            dracoQuantizationBitsLabel.Text = "Quantization";

            cancelButton.Text = "Cancel";

            okButton.Text = "Ok";

            mapZtoY.Text = "Map Rhino Z to glTF Y";

            OptionsToDialog();

            useDracoCompressionCheck.CheckedChanged += UseDracoCompressionCheck_CheckedChanged;
            dracoCompressionLevelInput.ValueChanged += DracoCompressionLevelInput_ValueChanged;
            dracoQuantizationBitsInputPosition.ValueChanged += DracoQuantizationBitsInputPosition_ValueChanged;
            dracoQuantizationBitsInputNormal.ValueChanged += DracoQuantizationBitsInputNormal_ValueChanged;
            dracoQuantizationBitsInputTexture.ValueChanged += DracoQuantizationBitsInputTexture_ValueChanged;

            mapZtoY.CheckedChanged += MapZtoY_CheckedChanged;

            cancelButton.Click += CancelButton_Click;
            okButton.Click += OkButton_Click;

            var gBox = new GroupBox() { Text = "Draco Quantization Bits" };
            gBox.Content = new TableLayout()
            {
                Padding = 5,
                Spacing = new Eto.Drawing.Size(2, 2),
                Rows = {
                    new TableRow("Position", "Normal", "Texture"),
                    new TableRow(dracoQuantizationBitsInputPosition, dracoQuantizationBitsInputNormal, dracoQuantizationBitsInputTexture)
                }
            };

            var layout = new DynamicLayout()
            {
                Padding = 5,
                Spacing = new Eto.Drawing.Size(2, 2)
            };
            layout.AddRow(mapZtoY, null);
            layout.AddSeparateRow(useDracoCompressionCheck, null);
            layout.AddSeparateRow(dracoCompressionLabel, dracoCompressionLevelInput, null);
            layout.AddSeparateRow(gBox, null);
            layout.AddSeparateRow(null, cancelButton, okButton);
            this.Content = layout;
        }

        private void MapZtoY_CheckedChanged(object sender, EventArgs e)
        {
            options.MapRhinoZToGltfY = mapZtoY.Checked.HasValue ? mapZtoY.Checked.Value : false;
        }

        private void OptionsToDialog()
        {
            useDracoCompressionCheck.Checked = options.UseDracoCompression;

            EnableDisableDracoControls(options.UseDracoCompression);

            dracoCompressionLevelInput.Value = options.DracoCompressionLevel;

            mapZtoY.Checked = options.MapRhinoZToGltfY;

            dracoQuantizationBitsInputPosition.Value = options.DracoQuantizationBitsPosition;
            dracoQuantizationBitsInputNormal.Value = options.DracoQuantizationBitsNormal;
            dracoQuantizationBitsInputTexture.Value = options.DracoQuantizationBitsTexture;
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
            bool useDraco = useDracoCompressionCheck.Checked.HasValue ? useDracoCompressionCheck.Checked.Value : false;

            options.UseDracoCompression = useDraco;

            EnableDisableDracoControls(useDraco);
        }

        private void DracoCompressionLevelInput_ValueChanged(object sender, EventArgs e)
        {
            int level = (int)dracoCompressionLevelInput.Value;

            options.DracoCompressionLevel = level;
        }

        private void DracoQuantizationBitsInputPosition_ValueChanged(object sender, EventArgs e)
        {
            int bits = (int)dracoQuantizationBitsInputPosition.Value;

            options.DracoQuantizationBitsPosition = bits;
        }

        private void DracoQuantizationBitsInputNormal_ValueChanged(object sender, EventArgs e)
        {
            int bits = (int)dracoQuantizationBitsInputNormal.Value;

            options.DracoQuantizationBitsNormal = bits;
        }

        private void DracoQuantizationBitsInputTexture_ValueChanged(object sender, EventArgs e)
        {
            int bits = (int)dracoQuantizationBitsInputTexture.Value;

            options.DracoQuantizationBitsTexture = bits;
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.Close(null);
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            this.Close(options);
        }

    }
}

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
        private NumericStepper dracoQuantizationBitsInput = new NumericStepper();
        
        private Button cancelButton = new Button();
        private Button okButton = new Button();

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
            dracoQuantizationBitsInput.DecimalPlaces = 0;
            dracoQuantizationBitsInput.MinValue = 8;
            dracoQuantizationBitsInput.MaxValue = 32;

            cancelButton.Text = "Cancel";

            okButton.Text = "Ok";

            OptionsToDialog();

            useDracoCompressionCheck.CheckedChanged += UseDracoCompressionCheck_CheckedChanged;
            dracoCompressionLevelInput.ValueChanged += DracoCompressionLevelInput_ValueChanged;
            dracoQuantizationBitsInput.ValueChanged += DracoQuantizationBitsInput_ValueChanged;

            cancelButton.Click += CancelButton_Click;
            okButton.Click += OkButton_Click;

            this.Content = new TableLayout()
            {
                Padding = 5,
                Spacing = new Eto.Drawing.Size(2, 2),
                Rows =
                {
                    new TableRow(useDracoCompressionCheck, null),
                    new TableRow(dracoCompressionLabel, dracoCompressionLevelInput),
                    new TableRow(dracoQuantizationBitsLabel, dracoQuantizationBitsInput),
                    new TableRow(cancelButton, okButton),
                    null,
                }
            };
        }

        private void OptionsToDialog()
        {
            useDracoCompressionCheck.Checked = options.UseDracoCompression;

            EnableDisableDracoControls(options.UseDracoCompression);

            dracoCompressionLevelInput.Value = options.DracoCompressionLevel;
            dracoQuantizationBitsInput.Value = options.DracoQuantizationBits;
        }

        private void EnableDisableDracoControls(bool enable)
        {
            dracoCompressionLevelInput.Enabled = enable;
            dracoQuantizationBitsInput.Enabled = enable;
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

        private void DracoQuantizationBitsInput_ValueChanged(object sender, EventArgs e)
        {
            int bits = (int)dracoQuantizationBitsInput.Value;

            options.DracoQuantizationBits = bits;
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

using System.Windows.Forms;

namespace MacroRecorderApp.Forms;

public class SaveMacroForm : Form
{
    private readonly TextBox _nameTextBox;

    public string MacroName => _nameTextBox.Text.Trim();

    public SaveMacroForm(string? initialName = null)
    {
        Text = "Сохранение макроса";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 320;
        Height = 200;

        var label = new Label
        {
            Text = "Сохранить макрос?",
            AutoSize = true,
            Left = 20,
            Top = 20
        };
        Controls.Add(label);

        _nameTextBox = new TextBox
        {
            Left = 20,
            Top = 60,
            Width = 260,
            Text = initialName ?? string.Empty
        };
        Controls.Add(_nameTextBox);

        var yesButton = new Button
        {
            Text = "Да",
            DialogResult = DialogResult.Yes,
            Left = 60,
            Width = 80,
            Top = 110
        };
        Controls.Add(yesButton);

        var noButton = new Button
        {
            Text = "Нет",
            DialogResult = DialogResult.No,
            Left = 160,
            Width = 80,
            Top = 110
        };
        Controls.Add(noButton);

        AcceptButton = yesButton;
        CancelButton = noButton;
    }
}

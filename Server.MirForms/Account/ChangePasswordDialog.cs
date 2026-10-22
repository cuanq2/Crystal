namespace Server
{
    public partial class ChangePasswordDialog : Form
    {
        public ChangePasswordDialog()
        {
            InitializeComponent();
            Server.MirForms.ServerUi.Apply(this);

            PasswordTextBox.MaxLength = Globals.MaxPasswordLength;
        }
    }
}

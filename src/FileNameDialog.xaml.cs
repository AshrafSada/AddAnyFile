using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MadsKristensen.AddAnyFile
{
    /// <summary>
    /// Interaction logic for FileNameDialog.xaml
    /// </summary>
    public partial class FileNameDialog : Window
    {
        private const string ICON_PATH = "pack://application:,,,/AddAnyFile;component/Resources/icon.png";
        private const string DEFAULT_TEXT = "enter folder(optional)/file with extension";

        private static readonly String[] _tips = new String[] {
            "Tip: 'folder/file.ext' also creates a new folder for the file",
            "Tip: You can create files starting with a dot, like '.gitignore'",
            "Tip: You can create files without file extensions, like 'LICENSE'",
            "Tip: Create folder by ending the name with a forward slash",
            "Tip: Use glob style syntax to add related files, like 'widget.(HTML,JS)'",
            "Tip: Separate names with commas to add multiple files and folders"
        };

        public string Input => txtFileName.Text.Trim();

        public FileNameDialog(string folderName)
        {
            InitializeComponent();
            lblFileFolder.Content = $"{folderName}/";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Icon = BitmapFrame.Create(new Uri(ICON_PATH, uriKind: UriKind.RelativeOrAbsolute));
            Title = Vsix.Name;
            SetTip();
            txtFileName.CaretIndex = 0;
            txtFileName.Text = DEFAULT_TEXT;
            txtFileName.Focus();
            txtFileName.Select(0, txtFileName.Text.Length);
        }

        private void SetTip()
        {
            Random rand = new Random(DateTime.Now.GetHashCode());
            int tipIndex = rand.Next(_tips.Length);
            lblTip.Content = _tips[tipIndex];
        }

        private void txtFileName_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (txtFileName.Text == DEFAULT_TEXT || String.IsNullOrWhiteSpace(txtFileName.Text))
                {
                    Close();
                }
                else
                {
                    txtFileName.Text = String.Empty;
                }
            }
            else if (txtFileName.Text == DEFAULT_TEXT)
            {
                txtFileName.Text = String.Empty;
                btnAddNew.IsEnabled = true;
            }
        }

        private void BtnAddNew_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
using System.Windows.Controls;

namespace RoxorChatBot
{
    /// <summary>
    /// Interaction logic for SongItem.xaml
    /// </summary>
    public partial class SongItem : UserControl
    {
        public SongItem()
        {
            InitializeComponent();
            btnDown.Tag = this;
            btnUp.Tag = this;
            songName.Tag = this;
        }
    }
}

/*
   Twitch chat bot for #RoXoRk0 
   
   App template by:﻿﻿﻿
   Houssem Dellai    
   houssem.dellai@ieee.org    
   +216 95 325 964    
   Studying Software Engineering    
   in the National Engineering School of Sfax (ENIS) 
   -------------------------------------------------- 
   Curently designed to run in debug mode.
   
   author: horato
   email: horato@seznam.cz
   github: http://github.com/horato
*/


using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Net;
using System.Windows.Threading;
using System.Threading;
using IrcDotNet;
using System.Windows.Input;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using HtmlAgilityPack;
using MahApps.Metro.Controls;

namespace RoxorChatBot
{
    public partial class MainWindow : MetroWindow
    {
        IrcClient c;
        List<DateTime> queue;
        System.Timers.Timer floodTimer;
        System.Timers.Timer songTimer;
        List<Video> playlist;

        public MainWindow()
        {
            InitializeComponent();

            Send_Button.IsEnabled = false;
            Disconnect_Button.IsEnabled = false;
            queue = new List<DateTime>();
            playlist = new List<Video>();
        }

        private void startSending_Click(object sender, RoutedEventArgs e)
        {
            startSending.IsEnabled = false;

            songTimer = new System.Timers.Timer(10000);
            songTimer.AutoReset = true;
            songTimer.Elapsed += (a, b) =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    SongsQueue items = null;
                    List<Video> videos = new List<Video>();
                    int inQueue = 0;
                    using (WebClient client = new WebClient())
                    {
                        try
                        {
                            string json = client.DownloadString("https://www.nightbot.tv/getsongs?channel=roxork0&rnd=" + Environment.TickCount);
                            items = new JavaScriptSerializer().Deserialize<SongsQueue>(json);
                        }
                        catch (Exception ee)
                        {
                            System.Diagnostics.Debug.WriteLine("songTimer error: " + ee.ToString());
                            return;
                        }
                    }

                    if (items.aaData == null || items.aaData.Count > 5)
                        return;

                    foreach (var song in items.aaData)
                    {
                        if (song[3].ToLower() == Properties.Settings.Default.twitch_login.ToLower())
                        {
                            inQueue++;

                            HtmlDocument document = new HtmlDocument();
                            Video v = new Video();

                            document.LoadHtml(song[1]);
                            HtmlNode node = document.DocumentNode;

                            HtmlAttributeCollection attributes = node.SelectNodes("//a[@href]")[0].Attributes;
                            foreach (HtmlAttribute attribute in attributes)
                                if (attribute.Name == "href")
                                    v.address = "https://www.youtube.com/watch?v=" + attribute.Value;
                            v.name = node.InnerText;
                            videos.Add(v);
                        }
                    }
                    tbVideosQueue.Text = "";

                    if (playlist.Count < 1)
                    {
                        stopSending_Click(null, null);
                        return;
                    }
                    if (inQueue < 2)
                    {
                        Video video = playlist[0];
                        playlist.RemoveAt(0);

                        sendChatMessage("!songrequest " + video.address);
                        tbVideosQueue.Text += video.name + Environment.NewLine;
                        tbVideos.Items.RemoveAt(0);
                    }
                    foreach (var v in videos)
                        tbVideosQueue.Text += v.name + Environment.NewLine;
                }));
            };
            songTimer.Start();
            stopSending.IsEnabled = true;
        }
        private void stopSending_Click(object sender, RoutedEventArgs e)
        {
            songTimer.Stop();
            stopSending.IsEnabled = false;
            startSending.IsEnabled = true;
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Properties.Settings.Default.twitch_login))
                Properties.Settings.Default.twitch_login = Prompt.ShowDialog("Specify twitch login name", "Login");
            if (string.IsNullOrWhiteSpace(Properties.Settings.Default.twitch_oauth))
                Properties.Settings.Default.twitch_oauth = Prompt.ShowDialog("Specify twitch oauth", "Oauth");
            if (string.IsNullOrWhiteSpace(Properties.Settings.Default.youtube_key))
                Properties.Settings.Default.youtube_key = Prompt.ShowDialog("Specify youtube api key", "Api key");
            if (string.IsNullOrWhiteSpace(Properties.Settings.Default.basePlaylist))
                Properties.Settings.Default.basePlaylist = Prompt.ShowDialog("Specify playlist ID", "Playlist id");
            Properties.Settings.Default.Save();

            tbStatus.Text = "Connecting...";
            Connect_Button.IsEnabled = false;

            new Thread(() =>
            {
                try
                {
                    c = new IrcClient();
                    var connectedEvent = new ManualResetEventSlim(false);
                    IPEndPoint point = new IPEndPoint(Dns.GetHostAddresses("irc.twitch.tv")[0], 6667);
                    c.Connected += (sender2, e2) => connectedEvent.Set();
                    c.RawMessageReceived += c_RawMessageReceived;
                    c.RawMessageSent += (arg1, arg2) =>
                    {
                        if (arg2 != null)
                            System.Diagnostics.Debug.WriteLine("sent " + arg2.Message.Command);
                        queue.Add(DateTime.Now);
                    };
                    c.Connect(point, false, new IrcUserRegistrationInfo()
                        {
                            UserName = Properties.Settings.Default.twitch_login,
                            NickName = Properties.Settings.Default.twitch_login,
                            Password = Properties.Settings.Default.twitch_oauth
                        });
                    if (!connectedEvent.Wait(10000))
                    {
                        c.Dispose();
                        System.Diagnostics.Debug.WriteLine("timed out");
                        return;
                    }

                    // if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.basePlaylist))
                    //    playlist = getVideosInPlaylist();

                    Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                    {
                        reDrawPlaylist();
                    }));

                    floodTimer = new System.Timers.Timer(1000);
                    floodTimer.AutoReset = true;
                    floodTimer.Elapsed += (arg1, arg2) =>
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                            {
                                List<DateTime> temp = new List<DateTime>();
                                foreach (var item in queue)
                                    temp.Add(item);
                                foreach (var item in temp)
                                    if (item.AddSeconds(30) < DateTime.Now)
                                        queue.Remove(item);
                            }));
                    };
                    floodTimer.Start();

                    c.SendRawMessage("JOIN #roxork0");

                    Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                    {
                        tbStatus.Text = "Connected";

                        Disconnect_Button.IsEnabled = true;
                        Send_Button.IsEnabled = true;
                        startSending.IsEnabled = true;
                        addSong.IsEnabled = true;
                    }));
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.ToString());
                    System.Diagnostics.Debug.WriteLine(exc.ToString());
                    Connect_Button.IsEnabled = true;
                }
            }).Start();
        }

        void songName_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Label btn = sender as Label;
            SongItem item = btn.Tag as SongItem;
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                tbVideos.Items.Remove(item);
                playlist.RemoveAll(x => x.address == ((string)item.Tag));
            }
            else
            {
                System.Diagnostics.Process.Start((string)item.Tag);
            }
        }
        void btnDown_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            SongItem item = btn.Tag as SongItem;
            int index = tbVideos.Items.IndexOf(item);

            if (index == tbVideos.Items.Count - 1)
                return;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                tbVideos.Items.Remove(item);
                tbVideos.Items.Add(item);

                Video vid = playlist.First(x => x.address == ((string)item.Tag));
                playlist.Remove(vid);
                playlist.Add(vid);
            }
            else
            {
                tbVideos.Items.Remove(item);
                tbVideos.Items.Insert(index + 1, item);

                Video vid = playlist.First(x => x.address == ((string)item.Tag));
                index = playlist.IndexOf(vid);
                playlist.Remove(vid);
                playlist.Insert(index + 1, vid);
            }
        }
        void btnUp_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            SongItem item = btn.Tag as SongItem;
            int index = tbVideos.Items.IndexOf(item);

            if (index == 0)
                return;

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                tbVideos.Items.Remove(item);
                tbVideos.Items.Insert(0, item);

                Video vid = playlist.First(x => x.address == ((string)item.Tag));
                playlist.Remove(vid);
                playlist.Insert(0, vid);
            }
            else
            {
                tbVideos.Items.Remove(item);
                tbVideos.Items.Insert(index - 1, item);

                Video vid = playlist.First(x => x.address == ((string)item.Tag));
                index = playlist.IndexOf(vid);
                playlist.Remove(vid);
                playlist.Insert(index - 1, vid);
            }
        }
        private List<Video> getVideosInPlaylist()
        {
            List<Item> items = loadPlaylist(Properties.Settings.Default.basePlaylist);
            List<Video> videos = convertPlaylist(items);
            return ShuffleList(videos);
        }

        private List<Video> convertPlaylist(List<Item> items)
        {
            List<Video> videos = new List<Video>();

            foreach (Item item in items)
            {
                Snippet snippet = item.snippet;
                if (!snippet.title.ToLower().StartsWith("nightcore") && snippet.resourceId.kind == "youtube#video")
                {
                    Video v = new Video();
                    v.name = snippet.title;
                    v.address = "https://www.youtube.com/watch?v=" + snippet.resourceId.videoId;
                    using (WebClient client = new WebClient())
                    {
                        string json = client.DownloadString("https://www.googleapis.com/youtube/v3/videos?part=contentDetails&id=" + snippet.resourceId.videoId + "&key=" + Properties.Settings.Default.youtube_key);
                        VideoInfoHeader info = new JavaScriptSerializer().Deserialize<VideoInfoHeader>(json);
                        if (info.pageInfo.totalResults == 0)
                            v.duration = new TimeSpan(999, 999, 999);
                        else
                            v.duration = DurationParser.GetDuration(info.items[0].contentDetails.duration);
                    }
                    if (v.duration.Hours == 0 && v.duration.Minutes < 6)
                        videos.Add(v);
                }
            }
            return videos;
        }

        private List<Item> loadPlaylist(string playlistID)
        {
            List<Item> ret = new List<Item>();
            List<PlaylistItemList> items = new List<PlaylistItemList>();
            string pageToken = "";
            while (pageToken != null)
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        string json;
                        if (pageToken == "")
                            json = client.DownloadString("https://www.googleapis.com/youtube/v3/playlistItems?part=snippet&playlistId=" + playlistID + "&key=" + Properties.Settings.Default.youtube_key + "&maxResults=50");
                        else
                            json = client.DownloadString("https://www.googleapis.com/youtube/v3/playlistItems?pageToken=" + pageToken + "&part=snippet&playlistId=" + playlistID + "&key=" + Properties.Settings.Default.youtube_key + "&maxResults=50");
                        PlaylistItemList x = new JavaScriptSerializer().Deserialize<PlaylistItemList>(json);

                        if (!string.IsNullOrWhiteSpace(x.nextPageToken))
                            pageToken = x.nextPageToken;
                        else
                            pageToken = null;

                        items.Add(x);
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e.ToString());
                }
            }
            foreach (PlaylistItemList x in items)
                ret.AddRange(x.items);
            return ret;
        }

        private List<E> ShuffleList<E>(List<E> inputList)
        {
            List<E> randomList = new List<E>();

            Random r = new Random();
            while (inputList.Count > 0)
            {
                int randomIndex = r.Next(0, inputList.Count);
                randomList.Add(inputList[randomIndex]);
                inputList.RemoveAt(randomIndex);
            }

            return randomList;
        }

        void c_RawMessageReceived(object sender, IrcRawMessageEventArgs e)
        {
            /*System.Diagnostics.Debug.Write("RawMessageReceived: Command: " + e.Message.Command + " Parameters: ");
            foreach (string s in e.Message.Parameters)
                if (!string.IsNullOrEmpty(s))
                    System.Diagnostics.Debug.Write(s + ",");*/
            if (e.RawContent != null)
                System.Diagnostics.Debug.WriteLine("RawMessageReceived: " + e.RawContent);

            if (e.Message.Command == "PRIVMSG" && e.Message.Parameters[0] == "#roxork0")
                handleRawMessage(e);
            else if (e.Message.Command == "PRIVMSG" && e.Message.Parameters[0] == "horatobot" && e.Message.Parameters[1] == "HISTORYEND roxork0")
                sendChatMessage("ItsBoshyTime KAPOW Keepo");
            //else if(e.Message.Command == "PART" && e.Message.Source.Name.ToLower().Contains("horatobot"))
             //   c.SendRawMessage("JOIN #roxork0");
        }

        private void handleRawMessage(IrcRawMessageEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                tbReceivedMsg.Text += e.Message.Source.Name + ": " + e.Message.Parameters[1] + Environment.NewLine;
                var oldFocusedElement = FocusManager.GetFocusedElement(this);

                tbReceivedMsg.Focus();
                tbReceivedMsg.CaretIndex = tbReceivedMsg.Text.Length;
                tbReceivedMsg.ScrollToEnd();

                FocusManager.SetFocusedElement(this, oldFocusedElement);
            }));
            if (e.Message.Parameters[1] == "!since")
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        string json = client.DownloadString("https://api.twitch.tv/kraken/users/" + e.Message.Source.Name + "/follows/channels/roxork0");
                        Followers followers = new JavaScriptSerializer().Deserialize<Followers>(json);
                        if (followers.status != 0)
                        {
                            System.Diagnostics.Debug.WriteLine("Error !since: error: " + followers.error + " Message: " + followers.message + ":::   " + "https://api.twitch.tv/kraken/users/" + e.Message.Source.Name + "/follows/channels/roxork0");
                        }
                        else
                        {
                            DateTime time = TimeParser.GetDuration(followers.created_at);
                            if (time.Month == 999)
                                System.Diagnostics.Debug.WriteLine("Error !since: parsing time: " + "https://api.twitch.tv/kraken/users/" + e.Message.Source.Name + "/follows/channels/roxork0");
                            else
                                sendChatMessage(e.Message.Source.Name + string.Format(" is following since {0}.{1:D2}.{2} {3}:{4:D2}:{5:D2}", time.Day, time.Month, time.Year, time.Hour, time.Minute, time.Second));
                        }

                    }
                }
                catch (Exception ee)
                {
                    System.Diagnostics.Debug.WriteLine(ee.ToString());
                }
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            if (queue.Count > 18)
                return;
            try
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
               {
                   sendChatMessage(tbMsg.Text);
                   tbMsg.Text = "";
               }));
            }
            catch (Exception exc) { MessageBox.Show(exc.ToString()); }
        }
        private void sendChatMessage(string message)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                tbReceivedMsg.Text += Properties.Settings.Default.twitch_login + ": " + message + Environment.NewLine;
                c.SendRawMessage("PRIVMSG #roxork0 :" + message);
            }));
        }
        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            if (queue.Count > 18)
                return;
            try
            {
                c.SendRawMessage("PART #roxork0");
                c.Disconnect();

                Disconnect_Button.IsEnabled = false;
                Connect_Button.IsEnabled = true;
                Send_Button.IsEnabled = false;
                startSending.IsEnabled = false;
                stopSending.IsEnabled = false;
                addSong.IsEnabled = false;
                tbMsg.Text = "";
                tbReceivedMsg.Text = "";
            }
            catch (Exception exc) { MessageBox.Show(exc.ToString()); }
        }

        private class Followers
        {
            public string created_at { get; set; }
            public Links _links { get; set; }
            public bool notifications { get; set; }
            public object channel { get; set; }
            public string error { get; set; }
            public int status { get; set; }
            public string message { get; set; }
        }

        private class Links
        {
            public string self { get; set; }
            public string next { get; set; }
        }
        private class SongsQueue
        {
            public List<string[]> aaData { get; set; }
        }

        private class Video
        {
            public string name { get; set; }
            public string address { get; set; }
            public TimeSpan duration { get; set; }
        }
        private class PlaylistItemList
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public string nextPageToken { get; set; }
            public string prevPageToken { get; set; }
            public PageInfo pageInfo { get; set; }
            public Item[] items { get; set; }
        }
        private class VideoInfoHeader
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public PageInfo pageInfo { get; set; }
            public VideoInfo[] items { get; set; }
        }
        private class VideoInfo
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public string id { get; set; }
            public ContentDetails contentDetails { get; set; }
        }
        private class ContentDetails
        {
            public string duration { get; set; }
            public string dimension { get; set; }
            public string definition { get; set; }
            public string caption { get; set; }
            public bool licensedContent { get; set; }
            public RegionRestriction regionRestriction { get; set; }
        }
        private class RegionRestriction
        {
            public List<string> blocked { get; set; }
        }
        private class PageInfo
        {
            public int totalResults { get; set; }
            public int resultsPerPage { get; set; }
        }
        private class Item
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public string id { get; set; }
            public Snippet snippet { get; set; }
        }
        private class Snippet
        {
            public string publishedAt { get; set; }
            public string channelId { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public Dictionary<string, Thumbnail> thumbnails { get; set; }
            public string channelTitle { get; set; }
            public string playlistId { get; set; }
            public int position { get; set; }
            public ResourceId resourceId { get; set; }

        }
        private class Thumbnail
        {
            public string url { get; set; }
            public int width { get; set; }
            public int height { get; set; }
        }
        private class ResourceId
        {
            public string kind { get; set; }
            public string videoId { get; set; }
        }

        private void addSong_Click(object sender, RoutedEventArgs e)
        {
            if (tbSongAddress.Visibility == Visibility.Visible)
            {
                if (!tbSongAddress.Text.ToLower().Contains("youtube") || (!tbSongAddress.Text.ToLower().Contains("v=") && !tbSongAddress.Text.ToLower().Contains("list=")))
                {
                    tbSongAddress.Visibility = Visibility.Hidden;
                    return;
                }

                if (!tbSongAddress.Text.ToLower().Contains("v="))
                {
                    string listID = tbSongAddress.Text.Split(new String[] { "list=" }, StringSplitOptions.None)[1].Split(new String[] { "&" }, StringSplitOptions.None)[0];
                    List<Item> items = loadPlaylist(listID);
                    List<Video> videos = ShuffleList(convertPlaylist(items));
                    Random rnd = new Random();
                    foreach (Video v in videos)
                        playlist.Insert(rnd.Next(0, playlist.Count == 0 ? 0 : playlist.Count - 1), v);
                    reDrawPlaylist();
                }
                else
                {
                    string videoID = tbSongAddress.Text.Split(new String[] { "v=" }, StringSplitOptions.None)[1].Split(new String[] { "&" }, StringSplitOptions.None)[0];
                    addsong(videoID);
                }
                tbSongAddress.Visibility = Visibility.Hidden;

            }
            else
            {
                tbSongAddress.Visibility = Visibility.Visible;
            }
        }

        private void reDrawPlaylist()
        {
            tbVideos.Items.Clear();
            foreach (Video v in playlist)
            {
                SongItem song = new SongItem();
                song.songName.Content = v.name;
                song.Tag = v.address;
                song.btnDown.Click += btnDown_Click;
                song.btnUp.Click += btnUp_Click;
                song.songName.MouseDoubleClick += songName_MouseDoubleClick;
                tbVideos.Items.Add(song);
            }
        }

        private void addsong(string videoID)
        {
            using (WebClient client = new WebClient())
            {
                string json = client.DownloadString("https://www.googleapis.com/youtube/v3/videos?part=snippet&id=" + videoID + "&key=" + Properties.Settings.Default.youtube_key);
                PlaylistItemList video = new JavaScriptSerializer().Deserialize<PlaylistItemList>(json);
                Snippet snippet = video.items[0].snippet;
                Video v = new Video();

                json = client.DownloadString("https://www.googleapis.com/youtube/v3/videos?part=contentDetails&id=" + videoID + "&key=" + Properties.Settings.Default.youtube_key);
                VideoInfoHeader info = new JavaScriptSerializer().Deserialize<VideoInfoHeader>(json);

                if (info.pageInfo.totalResults == 0)
                    v.duration = new TimeSpan(999, 999, 999);
                else
                    v.duration = DurationParser.GetDuration(info.items[0].contentDetails.duration);

                if (!snippet.title.ToLower().StartsWith("nightcore"))
                {
                    v.name = snippet.title;
                    v.address = "https://www.youtube.com/watch?v=" + videoID;

                    if (v.duration.Hours == 0 && v.duration.Minutes < 6)
                    {
                        SongItem song = new SongItem();
                        song.songName.Content = v.name;
                        song.Tag = v.address;
                        song.btnDown.Click += btnDown_Click;
                        song.btnUp.Click += btnUp_Click;
                        song.songName.MouseDoubleClick += songName_MouseDoubleClick;
                        tbVideos.Items.Insert(1, song);
                        playlist.Insert(1, v);
                    }
                }
            }
        }
    }
}

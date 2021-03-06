﻿using Microsoft.AspNetCore.SignalR.Client;
using SecureChat.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SecureChat.Client
{
    public static class TaskExtensions
    {
        public static async Task WithShownException(this Task a)
        {
            try
            {
                await a;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }
    
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        HubConnection connection;
        private LinkedList<char> _list;
        DateTime lastModified;
        string chatHistory;
        string filename = @"Key.txt";
        public MainWindow()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);

            InitializeComponent();

            string url = String.IsNullOrEmpty(UrlTextBox.Text) ? "http://localhost:54847/chathub" : UrlTextBox.Text;

            connection = new HubConnectionBuilder().WithUrl(url)
                .Build();

            var _ = connection.StartAsync().WithShownException(); 
            
            connection.On("ReceiveMessage", (string str, User user, DateTime dateTime) => ReceiveMessage(str, user, dateTime));
            connection.On("OnUserNameChanged", (User user) => OnUserNameChanged(user));

            SaveToList(filename).GetAwaiter().GetResult();
            UpdatingFileModifiedInfo(filename);

            connection.Closed += async (error) =>
            {
                MessageBox.Show(error.Message);
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };
        }
        private async void UpdatingFileModifiedInfo(string path)
        {
            while(true)
            {
                await Task.Delay(20);
                if (IsFileChanged(path, lastModified))
                {
                    await SaveToList(path);
                    UpdateCounterTextBox();
                    lastModified = File.GetLastWriteTime(path);
                }
            }
        }
        
        private string GetCifer(int lengthOfKey, bool shouldRemove)
        {
            if(_list.Count < lengthOfKey)
            {
                MessageBox.Show("Text is greater then {0}. Get new key and try again.", _list.Count.ToString());
                return null;
            }
            string cifer = new string(_list.TakeLast(lengthOfKey).ToArray());

            if (shouldRemove)
            {
                for (int i = 0; i < lengthOfKey; i++)
                {
                    _list.RemoveLast();
                }

                using (var fs = new FileStream(filename, FileMode.Open))
                {
                    fs.Seek(-lengthOfKey, SeekOrigin.End);
                    fs.SetLength(fs.Position);
                    fs.Close();
                }

                UpdateCounterTextBox();
            }

            return cifer;
        }
       

        private void ReceiveMessage(string str, User user, DateTime dateTime)
        {
            var cifer = GetCifer(str.Length, true);
            var decrypted = new string(CryptoAlgorithms.OneTimePad.encrypt(cifer, str).ToArray());
            chatHistory += Environment.NewLine + "[" + dateTime + "] " + user.Name + " (" + user.UserId + "): " + decrypted;
            ReceivedMessage.Text = chatHistory;
        }
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (! String.IsNullOrEmpty(NameTextBox.Text))
            {
                var cifer = GetCifer(ClientMessage.Text.Length, false); 
                var encrypted = new string(CryptoAlgorithms.OneTimePad.encrypt(cifer, ClientMessage.Text).ToArray()); 
                await connection.InvokeAsync("SendMessage", encrypted, NameTextBox.Text, DateTime.Now)
                    .WithShownException(); 
            }
            else
                MessageBox.Show("Please enter your name.");
            
        }
        
        private bool IsFileChanged(string path, DateTime dateTime) => File.GetLastWriteTime(path) != dateTime;

        private void OnUserNameChanged(User user)
        {
            chatHistory += Environment.NewLine + String.Format("User {0} has changed his username to {1}", user.UserId, user.Name);
            ReceivedMessage.Text = chatHistory;
        }

        private async Task SaveToList(string path) => _list = new LinkedList<char>((await File.ReadAllTextAsync(path)).ToArray<char>());
       
        private void UpdateCounterTextBox() => CounterTextBox.Text = _list.Count.ToString();
        
        private void OnClearing(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, ClearReceived))
                ReceivedMessage.Clear();
            else if (ReferenceEquals(sender, ClearClient))
                ClientMessage.Clear();                
        }
        static void MyHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            MessageBox.Show(e.Message);
        }
    }
}

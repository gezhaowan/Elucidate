﻿using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using FSharp.Literate;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.MDComment
{
    /// <summary>
    /// Interaction logic for MyControl.xaml
    /// </summary>
    public partial class MDView : UserControl
    {
        DTE2 dte;
        Events events;
        DocumentEvents docEvents;
        string sourceFile = "";
        int lastScrollHeight = 0;
        MDFormatter formatter = new MDFormatter();
        CancellationToken cancelFormattingTask = new CancellationToken();
        Task currentFormattingTask;

        public MDView()
        {
            InitializeComponent();
            dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            events = dte.Events;
            docEvents = events.DocumentEvents;
            dte.Events.WindowEvents.WindowActivated += OnWindowActivated;
            docEvents.DocumentSaved += OnDocumentSaved;
            browser.LoadCompleted += OnBrowserLoadCompleted;
            UpdateMarkdown("");
            Unloaded += (s,  o) => (formatter as IDisposable).Dispose();
        }

        
        private void SetButtonState(Button button, bool  enabled)
        {
            button.IsEnabled = enabled;
            button.Opacity = enabled ? 1.0 : 0.5;
        }

        private void OnBrowserLoadCompleted(object sender, NavigationEventArgs e)
        {
            SetBrowserScrollHeight(lastScrollHeight);
            // Don't allow going back to the previously local generated webpage file.
            // Back is only supported after navigating to a web url.
            SetButtonState(browseBackButton,browser.CanGoBack);
            SetButtonState(browseForwardButton, browser.CanGoForward);
        }

        IEnumerable<string> DumpException(Exception e)
        {
            if (e.InnerException != null)
                yield return String.Concat(DumpException(e.InnerException));
            yield return e.Message;
                
        }
        string WrapMessageInHtml(string msg)
        {
            return String.Format(@"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01 Transitional//EN"" ""http://www.w3.org/TR/html4/loose.dtd"">
<html><head><title>Elucidate Error</title></head><body><b>Error:</b> {0}</body></html>", msg);
        }

        int GetBrowserScrollHeight()
        {
            var htmlDoc = browser.Document as mshtml.HTMLDocument;
            if (htmlDoc != null)
            {
                var tags = htmlDoc.getElementsByTagName("HTML");
                return (tags.length > 0) ? tags.item(0).ScrollTop : 0;
            }
            return 0;
        }

        void SetBrowserScrollHeight(int height)
        {
            var htmlDoc = browser.Document as mshtml.HTMLDocument;
            if (htmlDoc != null)
            {
                htmlDoc.parentWindow.scrollTo(0, height);
            }
        }

        void UpdateMarkdown(string sourceFile)
        {
            if (currentFormattingTask != null && currentFormattingTask.Status == TaskStatus.Running)
            {
                // Ignore. We only allow one update at a time.
                return;
            }
            busyIndicator.IsBusy = true;
            currentFormattingTask = Task.Factory.StartNew(() => formatter.Format(sourceFile), cancelFormattingTask)
                .ContinueWith(t =>
                {
                    var outputFile = "";
                    if (t.Exception == null)
                    {
                        outputFile = t.Result;
                        lastScrollHeight = GetBrowserScrollHeight();
                        browser.Navigate(new Uri(String.Format("file:///{0}", outputFile)));
                    }
                    else
                    {
                        var msg = WrapMessageInHtml(String.Concat(DumpException(t.Exception)));
                        browser.NavigateToString(msg);
                   }
                    busyIndicator.IsBusy = false;
                    return outputFile;
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }
         
        private void OnDocumentSaved(Document Document)
        {
            if (sourceFile == Document.FullName)
            {
                UpdateMarkdown(sourceFile);
            }
        }

        private void OnWindowActivated(EnvDTE.Window GotFocus, EnvDTE.Window LostFocus)
        {
            var activated = dte.ActiveDocument;
            if (activated != null && sourceFile != activated.FullName && formatter.IsSupported(activated.Name))
            {
                SetBrowserScrollHeight(0);
                sourceFile = activated.FullName;
                UpdateMarkdown(sourceFile);
            }
        }

        void BrowseBack_Executed(object sender, RoutedEventArgs args)
        {
            if (browser.CanGoBack)
                browser.GoBack();
        }
        
        void BrowseForward_Executed(object sender, RoutedEventArgs args)
        {
            if (browser.CanGoForward)
                browser.GoForward();
        }
    }
}
using System;
using System.Windows;

namespace Common.Application
{
    public abstract class WpfApplicationContext : ApplicationContextBase
    {
        protected override int RunHosted()
        {
            return this.Application.Run(this.StartWindow);
        }

        protected override int RunUnhosted()
        {
            return 0;
        }

        public System.Windows.Application Application { get; private set; }
        public Window MainWindow { get; private set; }
        public Window SplashWindow { get; private set; }
        public Window StartWindow { get; private set; }
        public string MainWindowName { get; private set; }
        protected bool MainFormClosing { get; set; }
        //private Window BackgroundWindow { get; set; }
        protected override void OnLoad()
        {
            this.Application = OnCreateApplication();
            this.SplashWindow = OnCreateSplashWindow();

            if (this.SplashWindow != null)
            {
                //this.BackgroundWindow = new Window()
                //{
                //    Background = System.Windows.Media.Brushes.Black,
                //    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                //    WindowStyle = System.Windows.WindowStyle.None,
                //    WindowState = System.Windows.WindowState.Maximized,
                //    ShowInTaskbar = false,
                //    Topmost = true
                //};

                //this.BackgroundWindow.Show();

                this.SplashWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                this.SplashWindow.WindowStyle = WindowStyle.None;
                this.SplashWindow.ShowInTaskbar = false;
                this.SplashWindow.ShowActivated = true;
                this.SplashWindow.Topmost = true;
                this.StartWindow = this.SplashWindow;
                this.MainWindow = OnCreateMainWidow();
            }
            else
            {
                this.MainWindow = OnCreateMainWidow();
                this.StartWindow = this.MainWindow;
            }
            this.MainWindowName = this.MainWindow.Name;
            this.MainWindow.Loaded += MainWindow_Loaded;
            OnShowStartWindow();

            base.OnLoad();
        }

        protected abstract System.Windows.Application OnCreateApplication();
        protected abstract Window OnCreateMainWidow();
        protected virtual Window OnCreateSplashWindow() { return null; }


        protected override void OnLoadComplete()
        {
            if (this.SplashWindow != null)
            {
                this.SplashWindow.Dispatcher.Invoke(new Action(delegate ()
                {
                    this.MainWindow.Show();
                }));
            }
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            OnMainWindowLoaded();
        }

        protected virtual void OnMainWindowLoaded()
        {
            if (this.SplashWindow != null)
            {
                this.SplashWindow.Dispatcher.Invoke(new Action(delegate ()
                {
                    this.SplashWindow.Hide();
                    //this.BackgroundWindow.Close();
                }));
            }
        }


        protected virtual void OnShowStartWindow()
        {
            this.StartWindow.Show();
        }

        protected override bool OnExit(bool forced)
        {
            if (MainWindow != null)
            {
                MainWindow.Close();
            }
            if (this.SplashWindow != null)
            {
                SplashWindow.Close();
            }

            return true;
        }

        public override void Raise<T>(Subscription subscription, T args)
        {
            this.MainWindow.Dispatcher.Invoke(() =>
            {
                base.Raise(subscription, args);
            });
        }
    }
}

using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Media;
using System.Windows;
using System.Diagnostics;
using System.Drawing;

namespace ChatClient
{
    internal static class NotificationService
    {
        private static TaskbarIcon? _tray;

        public static bool IsAppActive { get; set; }
        public static string? ActiveDialogEmail { get; set; }

        public static bool NotificationsEnabled { get; set; } = true;
        public static bool SoundEnabled { get; set; } = true;
        public static bool BannerEnabled { get; set; } = true;

        // Вызываем один раз при старте приложения
        public static void InitTray()
        {
            if (_tray != null) return;

            _tray = new TaskbarIcon
            {
                ToolTipText = "ChatClient",
                Visibility = Visibility.Visible
            };

            try
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    _tray.Icon = Icon.ExtractAssociatedIcon(exe);
                }
            }
            catch
            {

            }
        }


        public static void Show(ChatMessageView msg)
        {
            if (!NotificationsEnabled) return;

            // умные уведомления
            if (IsAppActive &&
                !string.IsNullOrEmpty(ActiveDialogEmail) && string.Equals(ActiveDialogEmail, msg.FromEmail, StringComparison.OrdinalIgnoreCase))
                return;

            if (BannerEnabled)
            {
                InitTray();
                _tray?.ShowBalloonTip(msg.FromEmail, msg.IsFile ? $"Файл: {msg.FileName}" : msg.Text, BalloonIcon.Info);
            }
            if (SoundEnabled)
            {
                SystemSounds.Asterisk.Play();
            }
        }
    }
}

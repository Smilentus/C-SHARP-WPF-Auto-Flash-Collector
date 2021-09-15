using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Management;
using System.Threading;
using Microsoft.Win32;

namespace AutoFlashCollector
{
    public partial class Form1 : Form
    {
        // Константа названия программы в реестре
        const string appName = "AutoFlashCollector";

        string usbPath = @"Z:\";
        const string destPath = @"D:\Сортировочная папка\";
        public bool isReaded = false;

        private Thread capter;
        private ManagementEventWatcher watcher;
        private WqlEventQuery query;

        // Текущее состояние работы программы
        public enum _programState
        {
            None,
            Activation,
            Waiting,
            Copying,
        }

        public Form1()
        {
            InitializeComponent();

            this.ShowInTaskbar = true;
            notifyIcon.Visible = false;

            // КароЧИ перехватываем и пытаем события подключения устройств
            capter = new Thread(() =>
            {
                watcher = new ManagementEventWatcher();
                query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2");
                watcher.EventArrived += new EventArrivedEventHandler(watcher_EventArrived);
                watcher.Query = query;
                watcher.Start();
            });
            capter.Start();

            autoStartCheckBox.Checked = Properties.Settings.Default.autoStart;
        }

        public void SetProgramState(_programState state)
        {
            switch(state)
            {
                case _programState.Waiting:
                    labelInfo.Invoke(new Action(() => labelInfo.Text = "Состояние: Ожидание..."));
                    break;
                case _programState.Activation:
                    labelInfo.Invoke(new Action(() => labelInfo.Text = "Состояние: Активация..."));
                    break;
                case _programState.Copying:
                    labelInfo.Invoke(new Action(() => labelInfo.Text = "Состояние: Копирование..."));
                    break;
                case _programState.None:
                    labelInfo.Invoke(new Action(() => labelInfo.Text = "Состояние: Ожидание..."));
                    break;
            }
        }

        // Установка в регистр автозапуска программы
        public bool SetAutorunValue(bool autorun)
        {
            string ExePath = Application.ExecutablePath;
            RegistryKey reg;
            reg = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run\\");
            try
            {
                if (autorun)
                    reg.SetValue(appName, ExePath);
                else
                    reg.DeleteValue(appName);

                Properties.Settings.Default.autoStart = autorun;
                Properties.Settings.Default.Save();
                reg.Close();
            }
            catch
            {
                return false;
            }
            return true;
        }

        // Действие при подключении флэщьки
        private void watcher_EventArrived(object sender,
        EventArrivedEventArgs e)
        {
            // >>-->>-->> АВТООПРЕДЕЛЕНИЕ ФЛЕШКИ СДЕЛАТЬ <<--<<--<<
            List<string> drives = new List<string>();

            foreach(DriveInfo di in DriveInfo.GetDrives())
            {
                drives.Add(di.Name);
            }

            usbPath = drives[drives.Count - 1];

            if (!isReaded)
                AskForCopyFiles();
        }

        // Возвращает количество всех файлов по пути
        private int filesCountIn(string path)
        {
            int count = 0;

            foreach(string dirPath in Directory.GetDirectories(path, "*", 
                SearchOption.AllDirectories))
            {
                count++;
            }
            foreach (string newPath in Directory.GetFiles(usbPath, "*.*",
                SearchOption.AllDirectories))
            {
                count++;
            }

            return count;
        }

        // Спрашиваем нужно ли копировать файлы?
        private void AskForCopyFiles()
        {
            if (File.Exists(usbPath + "{auto}.txt"))
            {
                TakeFilesFromUSB();
            }
            else
            {
                DialogResult dr = MessageBox.Show($"Вы хотите скопировать все файлы с флешки тома {usbPath}?", "Подтверждение действия", MessageBoxButtons.YesNo);
                if (dr == DialogResult.Yes)
                {
                    TakeFilesFromUSB();
                }
                else if (dr == DialogResult.No)
                {
                    // Nothing to DO
                }
            }
        }

        // Получаем информацию о флешке
        private void InfoAboutDrive()
        {
            List<string> drives = new List<string>();

            string totalStr = "";

            foreach(DriveInfo di in DriveInfo.GetDrives())
            {
                drives.Add(di.Name + " - " + di.VolumeLabel);
                totalStr += Environment.NewLine + di.Name + " - " + di.VolumeLabel;
            }

            MessageBox.Show(totalStr);
        }

        // Забираем файлы с флешки
        private void TakeFilesFromUSB()
        {
            List<string> errorFiles = new List<string>();

            isReaded = true;

            SetProgramState(_programState.Copying);

            // Считаем сколько всего файлов
            int totalFiles = filesCountIn(usbPath);
            int copyiedFiles = 0;

            panelCopying.Invoke(new Action(() => panelCopying.Visible = true ));
            progressBar.Invoke(new Action(() => progressBar.Maximum = totalFiles));


            //Создать идентичную структуру папок
            foreach (string dirPath in Directory.GetDirectories(usbPath, "*",
                SearchOption.AllDirectories))
            {
                try
                {
                    Directory.CreateDirectory(dirPath.Replace(usbPath, destPath));
                    progressBar.Invoke(new Action(() => progressBar.Value++));
                    copyiedFiles++;
                    label2.Invoke(new Action(() => label2.Text = copyiedFiles + "/" + totalFiles));
                }
                catch (Exception e)
                {
                    errorFiles.Add(dirPath);
                }
            }

            // Копировать все файлы и перезаписать файлы с идентичным именем
            foreach (string newPath in Directory.GetFiles(usbPath, "*.*",
                SearchOption.AllDirectories))
            {
                try
                {
                    File.Copy(newPath, newPath.Replace(usbPath, destPath), true);
                    progressBar.Invoke(new Action(() => progressBar.Value++));
                    copyiedFiles++;
                    label2.Invoke(new Action(() => label2.Text = copyiedFiles + "/" + totalFiles));
                }
                catch (Exception e)
                {
                    errorFiles.Add(newPath);                }
            }

            MessageBox.Show(copyiedFiles + " файлов и папок было успешно перемещено в " + destPath + "!");

            if(errorFiles.Count > 0)
            {
                string str = "Ошибки в файлах: ";
                for(int i = 0; i < errorFiles.Count; i++)
                {
                    str += Environment.NewLine + errorFiles[i];
                }

                MessageBox.Show(str);
                errorFiles.Clear();
            }

            SetProgramState(_programState.Waiting);
            panelCopying.Invoke(new Action(() => panelCopying.Visible = false));
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            capter.Abort();
            watcher.Stop();
        }

        private void autoStartCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (autoStartCheckBox.Checked)
            {
                SetAutorunValue(true);
            }
            else
            {
                SetAutorunValue(false);
            }
        }

        private void notifyIcon_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                notifyIcon.Visible = false;
            }
        }

        private void Form1_Deactivate(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.ShowInTaskbar = false;
                notifyIcon.Visible = true;
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            SetProgramState(_programState.Waiting);
        }
    }
}

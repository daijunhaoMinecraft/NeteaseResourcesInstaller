using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NeteaseResourcesInstaller.Pages;

public class LogsPageViewModel : INotifyPropertyChanged
{
    private string _currentTask = "无任务";
    private string _currentState = "空闲";
    private double _progressValue;
    private bool _isIndeterminate;
    private string _logs = "";
    private string _message = "无消息";
    private bool _isInfoBarOpen = true;

    public string CurrentTask
    {
        get => _currentTask;
        set
        {
            _currentTask = value;
            OnPropertyChanged();
        }
    }

    public string CurrentState
    {
        get => _currentState;
        set
        {
            _currentState = value;
            OnPropertyChanged();
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            _progressValue = value;
            OnPropertyChanged();
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            _isIndeterminate = value;
            OnPropertyChanged();
        }
    }

    public string Logs
    {
        get => _logs;
        set
        {
            _logs = value;
            OnPropertyChanged();
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            OnPropertyChanged();
        }
    }

    public bool IsInfoBarOpen
    {
        get => _isInfoBarOpen;
        set
        {
            _isInfoBarOpen = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
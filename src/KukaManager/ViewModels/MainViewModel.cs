using CommunityToolkit.Mvvm.ComponentModel;

namespace KukaManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string currentPage = "工作台";
    [ObservableProperty] private string statusText = "离线运行 · 数据仅保存在此电脑";
    [ObservableProperty] private string searchText = "";
}

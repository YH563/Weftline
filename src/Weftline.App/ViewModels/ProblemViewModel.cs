using Avalonia.Media;
using Weftline.App.Models;

namespace Weftline.App.ViewModels;

public sealed class ProblemViewModel
{
    public ProblemViewModel(SampleProblem problem)
    {
        Code = problem.Code;
        Location = problem.Location;
        Message = problem.Message;

        (SeverityLabel, SeverityBrush) = problem.Severity switch
        {
            "error" => ("错误", new SolidColorBrush(Color.Parse("#E5484D"))),
            "warning" => ("警告", new SolidColorBrush(Color.Parse("#F5A524"))),
            _ => ("信息", new SolidColorBrush(Color.Parse("#3B82F6"))),
        };
    }

    public string SeverityLabel { get; }

    public IBrush SeverityBrush { get; }

    public string Code { get; }

    public string Location { get; }

    public string Message { get; }
}

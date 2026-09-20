namespace CiSmokeTest;

// Deliberately flawed fixture used only to verify the GitHub Actions review workflow end-to-end —
// the empty catch block and blocking .Wait() call below are each expected to surface as a Roslyn
// finding, so the workflow's "post to GitHub" step has something real to post. Not referenced by
// any .csproj, so it isn't compiled as part of the solution. Safe to delete once the workflow run
// has been confirmed to post a review on this PR.
public class ReviewTargetSample
{
    public void Run()
    {
        try
        {
            DoWork();
        }
        catch (Exception)
        {
        }
    }

    private void DoWork()
    {
        var task = System.Threading.Tasks.Task.Delay(100);
        task.Wait();
    }
}

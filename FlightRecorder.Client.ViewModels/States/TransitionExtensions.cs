namespace FlightRecorder.Client.ViewModels.States
{
    public static class TransitionExtensions
    {
        public static Transition ThenUpdate(this Transition state, MainViewModel viewModel)
            => state.Then(() => { viewModel.State = state.ToState; return true; });
    }
}

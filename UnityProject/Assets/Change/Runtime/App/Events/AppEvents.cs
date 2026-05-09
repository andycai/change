namespace Change.Runtime.App.Events
{
    public readonly struct AppLocaleChanged
    {
        public AppLocaleChanged(string cultureName) => CultureName = cultureName;
        public string CultureName { get; }
    }
}

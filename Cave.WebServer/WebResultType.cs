namespace Cave.Web
{
    /// <summary>
    /// Provides available result types
    /// </summary>
    public enum WebResultType
    {
        /// <summary>XML</summary>
        Xml = 1,

        /// <summary>HTML</summary>
        Html = 2,

        /// <summary>Plaintext</summary>
        Plain = 3,

        /// <summary>JSON</summary>
        Json = 4,

        /// <summary>Select by request extension</summary>
        Auto = 100,

        /// <summary>raw data</summary>
        Raw = 101,
    }
}

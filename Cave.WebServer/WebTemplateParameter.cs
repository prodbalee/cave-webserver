using Cave.Data;

namespace Cave.Web
{
    /// <summary>
    /// Provides function parameter name to template parameter name mapping.
    /// </summary>
    [Table("TemplateParameters")]
    public struct WebTemplateParameter
    {
        /// <summary>The identifier of the parameter.</summary>
        [Field(Flags = FieldFlags.ID)]
        public long ID;

        /// <summary>The function name.</summary>
        [Field]
        public string FunctionName;

        /// <summary>The template parameter name.</summary>
        [Field]
        public string ParameterAtTemplate;

        /// <summary>The function parameter name.</summary>
        [Field]
        public string ParameterAtFunction;
    }
}

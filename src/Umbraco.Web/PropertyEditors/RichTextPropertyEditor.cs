﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.Examine;
using Umbraco.Web.Composing;
using Umbraco.Web.Macros;
using Umbraco.Web.Templates;

namespace Umbraco.Web.PropertyEditors
{
    /// <summary>
    /// Represents a rich text property editor.
    /// </summary>
    [DataEditor(
        Constants.PropertyEditors.Aliases.TinyMce,
        "Rich Text Editor",
        "rte",
        ValueType = ValueTypes.Text,
        HideLabel = false,
        Group = Constants.PropertyEditors.Groups.RichContent,
        Icon = "icon-browser-window")]
    public class RichTextPropertyEditor : DataEditor
    {
        private IMediaService _mediaService;
        private IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;

        /// <summary>
        /// The constructor will setup the property editor based on the attribute if one is found
        /// </summary>
        public RichTextPropertyEditor(ILogger logger, IMediaService mediaService, IContentTypeBaseServiceProvider contentTypeBaseServiceProvider) : base(logger)
        {
            _mediaService = mediaService;
            _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        }

        /// <summary>
        /// Create a custom value editor
        /// </summary>
        /// <returns></returns>
        protected override IDataValueEditor CreateValueEditor() => new RichTextPropertyValueEditor(Attribute, _mediaService, _contentTypeBaseServiceProvider);

        protected override IConfigurationEditor CreateConfigurationEditor() => new RichTextConfigurationEditor();

        public override IPropertyIndexValueFactory PropertyIndexValueFactory => new RichTextPropertyIndexValueFactory();

        /// <summary>
        /// A custom value editor to ensure that macro syntax is parsed when being persisted and formatted correctly for display in the editor
        /// </summary>
        internal class RichTextPropertyValueEditor : DataValueEditor
        {
            private IMediaService _mediaService;
            private IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;

            public RichTextPropertyValueEditor(DataEditorAttribute attribute, IMediaService mediaService, IContentTypeBaseServiceProvider contentTypeBaseServiceProvider)
                : base(attribute)
            {
                _mediaService = mediaService;
                _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
            }

            /// <inheritdoc />
            public override object Configuration
            {
                get => base.Configuration;
                set
                {
                    if (value == null)
                        throw new ArgumentNullException(nameof(value));
                    if (!(value is RichTextConfiguration configuration))
                        throw new ArgumentException($"Expected a {typeof(RichTextConfiguration).Name} instance, but got {value.GetType().Name}.", nameof(value));
                    base.Configuration = value;

                    HideLabel = configuration.HideLabel;
                }
            }

            /// <summary>
            /// Format the data for the editor
            /// </summary>
            /// <param name="property"></param>
            /// <param name="dataTypeService"></param>Rte
            /// <param name="culture"></param>
            /// <param name="segment"></param>te
            public override object ToEditor(Property property, IDataTypeService dataTypeService, string culture = null, string segment = null)
            {
                var val = property.GetValue(culture, segment);
                if (val == null)
                    return null;

                var propertyValueWithMediaResolved = TemplateUtilities.ResolveMediaFromTextString(val.ToString());
                var parsed = MacroTagParser.FormatRichTextPersistedDataForEditor(propertyValueWithMediaResolved, new Dictionary<string, string>());
                return parsed;
            }

            /// <summary>
            /// Format the data for persistence
            /// </summary>
            /// <param name="editorValue"></param>
            /// <param name="currentValue"></param>
            /// <returns></returns>
            public override object FromEditor(Core.Models.Editors.ContentPropertyData editorValue, object currentValue)
            {
                if (editorValue.Value == null)
                    return null;

                var editorValueWithMediaUrlsRemoved = TemplateUtilities.RemoveMediaUrlsFromTextString(editorValue.Value.ToString());
                var parsed = MacroTagParser.FormatRichTextContentForPersistence(editorValueWithMediaUrlsRemoved);

                var userId = Current.UmbracoContext.Security.CurrentUser.Id;

                // TODO: In future task(get the parent folder from this config) to save the media into
                parsed = TemplateUtilities.FindAndPersistPastedTempImages(parsed, Constants.System.Root, userId, _mediaService, _contentTypeBaseServiceProvider);
                return parsed;
            }
        }

        internal class RichTextPropertyIndexValueFactory : IPropertyIndexValueFactory
        {
            public IEnumerable<KeyValuePair<string, IEnumerable<object>>> GetIndexValues(Property property, string culture, string segment, bool published)
            {
                var val = property.GetValue(culture, segment, published);

                if (!(val is string strVal)) yield break;

                //index the stripped HTML values
                yield return new KeyValuePair<string, IEnumerable<object>>(property.Alias, new object[] { strVal.StripHtml() });
                //store the raw value
                yield return new KeyValuePair<string, IEnumerable<object>>($"{UmbracoExamineIndex.RawFieldPrefix}{property.Alias}", new object[] { strVal });
            }
        }
    }


}

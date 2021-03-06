﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNet.Http;

using Moq;

using Scissorhands.Helpers;
using Scissorhands.Models.Posts;
using Scissorhands.Models.Settings;
using Scissorhands.Services.Exceptions;
using Scissorhands.Services.Tests.Fakes;
using Scissorhands.Services.Tests.Fixtures;
using Scissorhands.ViewModels.Post;

using Xunit;

namespace Scissorhands.Services.Tests
{
    /// <summary>
    /// This represents the test entity for the <see cref="PublishService"/> class.
    /// </summary>
    public class PublishServiceTest : IClassFixture<PublishServiceFixture>
    {
        private readonly string _defaultThemeName;
        private readonly Mock<WebAppSettings> _settings;
        private readonly Mock<ISiteMetadataSettings> _metadata;
        private readonly Mock<IFileHelper> _fileHelper;
        private readonly Mock<IHttpRequestHelper> _httpRequestHelper;
        private readonly IPublishService _service;

        private readonly string _filepath;
        private readonly Mock<HttpRequest> _request;

        private readonly Mock<PublishedMetadata> _publishedMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishServiceTest"/> class.
        /// </summary>
        /// <param name="fixture"><see cref="PublishServiceFixture"/> instance.</param>
        public PublishServiceTest(PublishServiceFixture fixture)
        {
            this._defaultThemeName = fixture.DefaultThemeName;
            this._settings = fixture.WebAppSettings;
            this._metadata = fixture.SiteMetadataSettings;
            this._fileHelper = fixture.FileHelper;
            this._httpRequestHelper = fixture.HttpRequestHelper;
            this._service = fixture.PublishService;

            this._filepath = $"{Path.GetTempPath()}/home/scissorhands.net/wwwroot/posts".Replace('/', Path.DirectorySeparatorChar);
            this._request = new Mock<HttpRequest>();

            this._publishedMetadata = fixture.PublishedMetadata;
        }

        /// <summary>
        /// Tests whether constructor should throw an exception or not.
        /// </summary>
        [Fact]
        public void Given_NullParameter_Constructor_ShouldThrow_ArgumentNullException()
        {
            Action action1 = () => { var service = new PublishService(null, this._metadata.Object,  this._fileHelper.Object, this._httpRequestHelper.Object); };
            action1.ShouldThrow<ArgumentNullException>();

            Action action2 = () => { var service = new PublishService(this._settings.Object, null, this._fileHelper.Object, this._httpRequestHelper.Object); };
            action2.ShouldThrow<ArgumentNullException>();

            Action action3 = () => { var service = new PublishService(this._settings.Object, this._metadata.Object, null, this._httpRequestHelper.Object); };
            action3.ShouldThrow<ArgumentNullException>();

            Action action4 = () => { var service = new PublishService(this._settings.Object, this._metadata.Object, this._fileHelper.Object, null); };
            action4.ShouldThrow<ArgumentNullException>();
        }

        /// <summary>
        /// Tests whether constructor should NOT throw an exception or not.
        /// </summary>
        [Fact]
        public void Given_Parameters_Constructor_ShouldThrow_NoException()
        {
            Action action = () => { var service = new PublishService(this._settings.Object, this._metadata.Object, this._fileHelper.Object, this._httpRequestHelper.Object); };
            action.ShouldNotThrow<Exception>();
        }

        /// <summary>
        /// Tests whether ApplyMetadata should throw an exception or not.
        /// </summary>
        [Fact]
        public void Given_NullParameter_ApplyMetadata_ShouldThrow_ArgumentNullException()
        {
            Action action1 = () => { var result = this._service.ApplyMetadata(null, this._publishedMetadata.Object); };
            action1.ShouldThrow<ArgumentNullException>();

            Action action2 = () => { var result = this._service.ApplyMetadata(new PostFormViewModel(), null); };
            action2.ShouldThrow<ArgumentNullException>();
        }

        /// <summary>
        /// Tests whether ApplyMetadata should return result or not.
        /// </summary>
        /// <param name="title">Title value.</param>
        /// <param name="slug">Slug value.</param>
        /// <param name="author">Author value.</param>
        /// <param name="tags">List of tags.</param>
        /// <param name="body">Content value.</param>
        [Theory]
        [InlineData("Hello World", "hello-world", "Joe Bloggs", "hello,world", "**Hello World**")]
        public void Given_Model_ApplyMetadata_ShouldReturn_Result(string title, string slug, string author, string tags, string body)
        {
            var model = new PostFormViewModel()
                            {
                                Title  = title,
                                Slug   = slug,
                                Author = author,
                                Tags   = tags,
                                Body   = body
                            };
            var markdown = this._service.ApplyMetadata(model, this._publishedMetadata.Object);

            markdown.Should().StartWithEquivalent("---");
            markdown.Should().ContainEquivalentOf($"* Title: {title}");
            markdown.Should().ContainEquivalentOf($"* Slug: {slug}");
            markdown.Should().ContainEquivalentOf($"* Author: {author}");
            markdown.Should().ContainEquivalentOf($"* Tags: {string.Join(", ", tags.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries))}");
            markdown.Should().EndWithEquivalent($"{body}{Environment.NewLine}");
        }

        /// <summary>
        /// Tests whether the method should throw an exception or not.
        /// </summary>
        [Fact]
        public void Given_NullMarkdown_PublishMarkdownAsync_ShouldThrow_ArgumentNullException()
        {
            Func<Task> func1 = async () => { var result = await this._service.PublishMarkdownAsync(null, this._publishedMetadata.Object).ConfigureAwait(false); };
            func1.ShouldThrow<ArgumentNullException>();

            var markdown = "**Hello World**";
            Func<Task> func2 = async () => { var result = await this._service.PublishMarkdownAsync(markdown, null).ConfigureAwait(false); };
            func2.ShouldThrow<ArgumentNullException>();
        }

        /// <summary>
        /// Tests whether the method should throw an exception or not.
        /// </summary>
        /// <param name="markdown">Markdown value.</param>
        [Theory]
        [InlineData("**Hello World**")]
        public void Given_FalseWritingSync_PublishMarkdownAsync_ShouldThrow_PublishFailedException(string markdown)
        {
            this._fileHelper.Setup(p => p.GetDirectory(It.IsAny<string>())).Returns(this._filepath);
            this._fileHelper.Setup(p => p.WriteAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(false));

            Func<Task> func = async () => { var result = await this._service.PublishMarkdownAsync(markdown, this._publishedMetadata.Object).ConfigureAwait(false); };
            func.ShouldThrow<PublishFailedException>();
        }

        /// <summary>
        /// Tests whether the method should return value or not.
        /// </summary>
        /// <param name="markdown">Markdown value.</param>
        [Theory]
        [InlineData("**Hello World**")]
        public async void Given_Markdown_PublishMarkdownAsync_ShouldReturn_Filepath(string markdown)
        {
            this._fileHelper.Setup(p => p.GetDirectory(It.IsAny<string>())).Returns(this._filepath);
            this._fileHelper.Setup(p => p.WriteAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(true));

            var result = await this._service.PublishMarkdownAsync(markdown, this._publishedMetadata.Object).ConfigureAwait(false);
            var markdownpath = $"{this._settings.Object.MarkdownPath}/{this._publishedMetadata.Object.DatePublished.ToString("yyyy/MM/dd")}/{this._publishedMetadata.Object.Slug}.md";
            result.Should().Be(markdownpath);
        }

        /// <summary>
        /// Tests whether the method should throw an exception or not.
        /// </summary>
        [Fact]
        public void Given_NullHtml_PublishHtmlAsync_ShouldThrow_ArgumentNullException()
        {
            Func<Task> func = async () => { var result = await this._service.PublishHtmlAsync(null, this._publishedMetadata.Object).ConfigureAwait(false); };
            func.ShouldThrow<ArgumentNullException>();
        }

        /// <summary>
        /// Tests whether the method should throw an exception or not.
        /// </summary>
        /// <param name="markdown">Markdown value.</param>
        [Theory]
        [InlineData("**Hello World**")]
        public void Given_FalseWritingSync_PublishHtmlAsync_ShouldThrow_PublishFailedException(string markdown)
        {
            this._fileHelper.Setup(p => p.GetDirectory(It.IsAny<string>())).Returns(this._filepath);
            this._fileHelper.Setup(p => p.WriteAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(false));

            Func<Task> func = async () => { var result = await this._service.PublishHtmlAsync(markdown, this._publishedMetadata.Object).ConfigureAwait(false); };
            func.ShouldThrow<PublishFailedException>();
        }

        /// <summary>
        /// Tests whether the method should return value or not.
        /// </summary>
        /// <param name="html">HTML value.</param>
        [Theory]
        [InlineData("<strong>Hello World</strong>")]
        public async void Given_Markdown_PublishHtmlAsync_ShouldReturn_Filepath(string html)
        {
            this._fileHelper.Setup(p => p.GetDirectory(It.IsAny<string>())).Returns(this._filepath);
            this._fileHelper.Setup(p => p.WriteAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(true));

            var result = await this._service.PublishHtmlAsync(html, this._publishedMetadata.Object).ConfigureAwait(false);
            var htmlpath = $"{this._settings.Object.HtmlPath}/{this._publishedMetadata.Object.DatePublished.ToString("yyyy/MM/dd")}/{this._publishedMetadata.Object.Slug}.html";
            result.Should().Be(htmlpath);
        }

        /// <summary>
        /// Tests whether the method should throw an exception or not.
        /// </summary>
        [Fact]
        public void Given_NullParameter_GetPublishedHtmlAsync_ShouldThrow_ArgumentNullException()
        {
            Func<Task> func1 = async () => { var html = await this._service.GetPublishedHtmlAsync(null, this._request.Object).ConfigureAwait(false); };
            func1.ShouldThrow<ArgumentNullException>();

            var model = new PostFormViewModel();
            Func<Task> func2 = async () => { var html = await this._service.GetPublishedHtmlAsync(model, null).ConfigureAwait(false); };
            func2.ShouldThrow<ArgumentNullException>();
        }

        /// <summary>
        /// Tests whether the method should return result or not.
        /// </summary>
        /// <param name="markdown">Markdown string.</param>
        /// <param name="html">HTML string.</param>
        [Theory]
        [InlineData("**Hello World**", "<strong>Hello World</strong>")]
        public async void Given_Parameters_GetPublishedHtmlAsync_ShouldReturn_Html(string markdown, string html)
        {
            this._metadata.SetupGet(p => p.Theme).Returns(this._defaultThemeName);

            var message = new HttpResponseMessage { Content = new StringContent(html) };

            var handler = new HttpResponseHandlerFake(message);
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080") };

            this._httpRequestHelper.Setup(p => p.CreateHttpClient(It.IsAny<HttpRequest>(), It.IsAny<HttpMessageHandler>())).Returns(client);

            var content = new StringContent(html, Encoding.UTF8);
            this._httpRequestHelper.Setup(p => p.CreateHttpClient(It.IsAny<HttpRequest>(), It.IsAny<PublishMode>(), It.IsAny<HttpMessageHandler>())).Returns(client);
            this._httpRequestHelper.Setup(p => p.CreateStringContent(It.IsAny<object>())).Returns(content);

            var model = new PostFormViewModel()
                            {
                                Title = "Title",
                                Slug = "slug",
                                Author = "Joe Bloggs",
                                Tags = "tag1,tag2",
                                Body = markdown
                            };
            var result = await this._service.GetPublishedHtmlAsync(model, this._request.Object).ConfigureAwait(false);
            result.Should().Be(html);
        }

        /// <summary>
        /// Tests whether the method should throw an exception or not.
        /// </summary>
        [Fact]
        public void Given_NullParameter_PublishPostAsync_ShouldThrow_ArgumentNullException()
        {
            var model = new PostFormViewModel();

            Func<Task> func1 = async () => { var result = await this._service.PublishPostAsync(null, this._request.Object).ConfigureAwait(false); };
            func1.ShouldThrow<ArgumentNullException>();

            Func<Task> func2 = async () => { var result = await this._service.PublishPostAsync(model, null).ConfigureAwait(false); };
            func2.ShouldThrow<ArgumentNullException>();
        }

        /// <summary>
        /// Tests whether the method should return result or not.
        /// </summary>
        [Fact]
        public async void Given_Parameters_PublishPostAsync_ShouldReturn_Result()
        {
            var model = new PostFormViewModel()
                            {
                                Title = "Hello World",
                                Slug = "hello-world",
                                Author = "Joe Bloggs",
                                Tags = "hello,world",
                                Body = "**Hello World**",
                                DatePublished = DateTime.Now,
                            };
            var html = "<strong>Hello World</strong>";

            this._fileHelper.Setup(p => p.GetDirectory(It.IsAny<string>())).Returns(this._filepath);
            this._fileHelper.Setup(p => p.WriteAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(true));

            this._metadata.SetupGet(p => p.Theme).Returns(this._defaultThemeName);

            var message = new HttpResponseMessage { Content = new StringContent(html) };

            var handler = new HttpResponseHandlerFake(message);
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5080") };

            this._httpRequestHelper.Setup(p => p.CreateHttpClient(It.IsAny<HttpRequest>(), It.IsAny<HttpMessageHandler>())).Returns(client);

            var content = new StringContent(html, Encoding.UTF8);
            this._httpRequestHelper.Setup(p => p.CreateHttpClient(It.IsAny<HttpRequest>(), It.IsAny<PublishMode>(), It.IsAny<HttpMessageHandler>())).Returns(client);
            this._httpRequestHelper.Setup(p => p.CreateStringContent(It.IsAny<object>())).Returns(content);

            var markdownpath = $"{this._settings.Object.MarkdownPath}/{this._publishedMetadata.Object.DatePublished.ToString("yyyy/MM/dd")}/{this._publishedMetadata.Object.Slug}.md";
            var htmlpath = $"{this._settings.Object.HtmlPath}/{this._publishedMetadata.Object.DatePublished.ToString("yyyy/MM/dd")}/{this._publishedMetadata.Object.Slug}.html";

            var publishedpath = await this._service.PublishPostAsync(model, this._request.Object).ConfigureAwait(false);
            publishedpath.Markdown.Should().BeEquivalentTo(markdownpath);
            publishedpath.Html.Should().BeEquivalentTo(htmlpath);
        }
    }
}
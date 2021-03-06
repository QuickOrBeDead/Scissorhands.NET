﻿using System;
using System.Net;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNet.Http;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.ViewFeatures;
using Microsoft.Extensions.PlatformAbstractions;

using Moq;

using Scissorhands.Helpers;
using Scissorhands.Models.Posts;
using Scissorhands.Models.Settings;
using Scissorhands.Services;
using Scissorhands.ViewModels.Post;
using Scissorhands.WebApp.Controllers;
using Scissorhands.WebApp.Tests.Fixtures;

using Xunit;

namespace Scissorhands.WebApp.Tests
{
    /// <summary>
    /// This represents the test entity for the <see cref="PostController"/> class.
    /// </summary>
    public class PostControllerTest : IClassFixture<PostControllerFixture>
    {
        private readonly string _defaultThemeName;
        private readonly Mock<IMarkdownService> _markdownService;
        private readonly Mock<IViewModelService> _viewModelService;
        private readonly Mock<IPublishService> _publishService;
        private readonly PostController _controller;

        private readonly Mock<IApplicationEnvironment> _applicationEnvironment;
        private readonly Mock<IUrlHelper> _urlHelper;
        private readonly Mock<IServiceProvider> _requestServices;
        private readonly Mock<HttpContext> _httpContext;
        private readonly ActionContext _actionContext;
        private readonly Mock<ITempDataDictionary> _tempData;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostControllerTest"/> class.
        /// </summary>
        /// <param name="fixture"><see cref="PostControllerFixture"/> instance.</param>
        public PostControllerTest(PostControllerFixture fixture)
        {
            this._defaultThemeName = fixture.DefaultThemeName;
            this._markdownService = fixture.MarkdownService;
            this._viewModelService = fixture.ViewModelService;
            this._publishService = fixture.PublishService;
            this._controller = fixture.Controller;

            this._applicationEnvironment = fixture.ApplicationEnvironment;
            this._urlHelper = fixture.UrlHelper;
            this._requestServices = fixture.RequestServices;
            this._httpContext = fixture.HttpContext;
            this._actionContext = fixture.ActionContext;
            this._tempData = fixture.TempData;
        }

        /// <summary>
        /// Tests whether the constructor throws an exception or not.
        /// </summary>
        [Fact]
        public void Given_NullParameter_Constructor_ShouldThrow_ArgumentNullException()
        {
            Action action3 = () => { var controller = new PostController(null, this._viewModelService.Object, this._publishService.Object); };
            action3.ShouldThrow<ArgumentNullException>();

            Action action4 = () => { var controller = new PostController(this._markdownService.Object, null, this._publishService.Object); };
            action4.ShouldThrow<ArgumentNullException>();

            Action action5 = () => { var controller = new PostController(this._markdownService.Object, this._viewModelService.Object, null); };
            action5.ShouldThrow<ArgumentNullException>();
        }

        /// <summary>
        /// Tests whether the action should return <see cref="RedirectToRouteResult"/> instance or not.
        /// </summary>
        [Fact]
        public void Given_Index_ShouldReturn_RedirectToRouteResult()
        {
            this._requestServices.Setup(p => p.GetService(typeof(IUrlHelper))).Returns(this._urlHelper.Object);

            var result = this._controller.Index() as RedirectToRouteResult;
            result.Should().NotBeNull();
            result.RouteName.Should().Be("write");
        }

        /// <summary>
        /// Tests whether the action should return <see cref="ViewResult"/> instance or not.
        /// </summary>
        [Fact]
        public void Given_Write_ShouldReturn_ViewResult()
        {
            var model = new PostFormViewModel();
            this._viewModelService.Setup(p => p.CreatePostFormViewModel(It.IsAny<HttpRequest>())).Returns(model);

            var result = this._controller.Write() as ViewResult;
            result.Should().NotBeNull();

            var vm = result.ViewData.Model as PostFormViewModel;
            vm.Should().NotBeNull();
        }

        /// <summary>
        /// Tests whether the action should return <see cref="HttpStatusCodeResult"/> instance or not.
        /// </summary>
        [Fact]
        public void Given_NullParameter_Preview_ShouldReturn_BadRequest()
        {
            var result = this._controller.Preview(null) as HttpStatusCodeResult;
            result.Should().NotBeNull();
            result.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Tests whether the action should return <see cref="ViewResult"/> instance or not.
        /// </summary>
        /// <param name="markdown">String value in Markdown format.</param>
        /// <param name="html">String value in HTML format.</param>
        [Theory]
        [InlineData("**Hello World**", "<p>Joe Bloggs</p>")]
        public void Given_Model_Preview_ShouldReturn_ViewResult(string markdown, string html)
        {
            this._markdownService.Setup(p => p.Parse(It.IsAny<string>())).Returns(html);

            var ppvm = new PostPreviewViewModel() { Theme = this._defaultThemeName };
            var pms = new PageMetadataSettings();
            this._viewModelService.Setup(p => p.CreatePostPreviewViewModel()).Returns(ppvm);
            this._viewModelService.Setup(p => p.CreatePageMetadata(It.IsAny<PostFormViewModel>(), It.IsAny<HttpRequest>(), It.IsAny<PublishMode>())).Returns(pms);

            var model = new PostFormViewModel() { Title = "Title", Slug = "slug", Body = markdown };

            var result = this._controller.Preview(model) as ViewResult;
            result.Should().NotBeNull();

            var vm = result.ViewData.Model as PostPreviewViewModel;
            vm.Should().NotBeNull();

            vm.Theme.Should().Be(this._defaultThemeName);
            vm.Html.Should().Be(html);
        }

        /// <summary>
        /// Tests whether the action should return <see cref="HttpStatusCodeResult"/> instance or not.
        /// </summary>
        [Fact]
        public async void Given_NullParameter_Publish_ShouldReturn_BadRequest()
        {
            var result = await this._controller.Publish(null).ConfigureAwait(false) as HttpStatusCodeResult;
            result.Should().NotBeNull();
            result.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Tests whether the action should return <see cref="ViewResult"/> instance or not.
        /// </summary>
        /// <param name="markdown">String value in Markdown format.</param>
        /// <param name="html">String value in HTML format.</param>
        /// <param name="markdownpath">Path of the Markdown file.</param>
        /// <param name="htmlpath">Path of the HTML post file.</param>
        [Theory]
        [InlineData("**Hello World**", "<p>Joe Bloggs</p>", "/posts/markdown.md", "/posts/post.html")]
        public async void Given_Model_Publish_ShouldReturn_ViewResult(string markdown, string html, string markdownpath, string htmlpath)
        {
            this._requestServices.Setup(p => p.GetService(typeof(IApplicationEnvironment))).Returns(this._applicationEnvironment.Object);

            this._httpContext.SetupGet(p => p.RequestServices).Returns(this._requestServices.Object);

            this._controller.ActionContext = this._actionContext;
            this._controller.TempData = this._tempData.Object;

            var ppvm = new PostPublishViewModel() { Theme = this._defaultThemeName };
            var pms = new PageMetadataSettings();
            this._viewModelService.Setup(p => p.CreatePostPublishViewModel()).Returns(ppvm);
            this._viewModelService.Setup(p => p.CreatePageMetadata(It.IsAny<PostFormViewModel>(), It.IsAny<HttpRequest>(), It.IsAny<PublishMode>())).Returns(pms);

            var publishedpath = new PublishedPostPath() { Markdown = markdownpath, Html = htmlpath };
            this._publishService.Setup(p => p.PublishPostAsync(It.IsAny<PostFormViewModel>(), It.IsAny<HttpRequest>())).Returns(Task.FromResult(publishedpath));

            var model = new PostFormViewModel() { Title = "Title", Slug = "slug", Body = markdown };

            var result = await this._controller.Publish(model).ConfigureAwait(false) as ViewResult;
            result.Should().NotBeNull();

            var vm = result.ViewData.Model as PostPublishViewModel;
            vm.Should().NotBeNull();

            vm.Theme.Should().Be(this._defaultThemeName);
            vm.MarkdownPath.Should().Be(markdownpath);
            vm.HtmlPath.Should().Be(htmlpath);
        }

        /// <summary>
        /// Tests whether the action should return <see cref="HttpStatusCodeResult"/> instance or not.
        /// </summary>
        [Fact]
        public void Given_NullModel_PublishHtml_ShouldReturn_BadRequest()
        {
            var result = this._controller.PublishHtml(null) as HttpStatusCodeResult;
            result.Should().NotBeNull();
            result.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Tests whether the action should return <see cref="ViewResult"/> instance or not.
        /// </summary>
        /// <param name="markdown">String value in Markdown format.</param>
        /// <param name="html">String value in HTML format.</param>
        [Theory]
        [InlineData("**Hello World**", "<strong>Hello World</strong>")]
        public void Given_Model_PublishHtml_ShouldReturn_ViewResult(string markdown, string html)
        {
            var model = new PostFormViewModel()
                            {
                                Title = "Title",
                                Slug = "slug",
                                Author = "Joe Bloggs",
                                Tags = "tag1,tag2",
                                Body = markdown
                            };

            this._markdownService.Setup(p => p.Parse(It.IsAny<string>())).Returns(html);

            var ppvm = new PostParseViewModel() { Theme = this._defaultThemeName };
            var pms = new PageMetadataSettings();
            this._viewModelService.Setup(p => p.CreatePostParseViewModel()).Returns(ppvm);
            this._viewModelService.Setup(p => p.CreatePageMetadata(It.IsAny<PostFormViewModel>(), It.IsAny<HttpRequest>(), It.IsAny<PublishMode>())).Returns(pms);

            var result = this._controller.PublishHtml(model) as ViewResult;
            result.Should().NotBeNull();

            var vm = result.ViewData.Model as PostParseViewModel;
            vm.Should().NotBeNull();
            vm.Html.Should().ContainEquivalentOf(html);
        }
    }
}
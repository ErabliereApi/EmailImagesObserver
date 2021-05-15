using AzureComputerVision;
using BlazorApp.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorApp.Pages
{
    public partial class ImageAnalysis : IObserver<ImageInfo>, IDisposable
    {
        public Guid ClientSessionId { get; } = Guid.NewGuid();

        [Inject]
        private ILogger<ImageAnalysis> Logger { get; set; }

        [Inject]
        private IJSRuntime JS { get; set; }

        [Inject]
        private ImageInfoService ImageInfoService { get; set; }

        [Inject]
        private IdleClient idleClient { get; set; }


        private IList<ImageInfo>? imageInfo;

        private int DeleteId;

        private int skip = 0;

        [Parameter]
        public string? SearchTerms { get; set; }

        protected override async Task OnInitializedAsync()
        {
            imageInfo = (await ImageInfoService.GetImageInfo(take: 15, skip: skip, SearchTerms)).ToList();

            idleClient.Subscribe(this);
        }

        protected void ConfirmDelete(int id, string title)
        {
            DeleteId = id;

            JS.InvokeAsync<bool>("confirmDelete", title);
        }

        protected async Task DeleteImageInfo()
        {
            await JS.InvokeAsync<bool>("hideDeleteDialog");

            ImageInfoService.DeleteImageInfo(DeleteId);
            await OnInitializedAsync();
        }

        protected async Task TriggerSearch()
        {
            imageInfo = null;

            imageInfo = (await ImageInfoService.GetImageInfo(take: 15, skip: skip, SearchTerms)).ToList();
        }

        public void OnCompleted()
        {
            Logger.LogInformation("Observer has finisehd sending push-back notification");
        }

        public void OnError(Exception error)
        {
            Logger.LogError(error, error.Message);
        }

        public void OnNext(ImageInfo value)
        {
            InvokeAsync(() =>
            {
                if (SearchTerms == null && skip == 0)
                {
                    var list = new List<ImageInfo>(imageInfo.Count + 1);

                    list.Add(value);
                    list.AddRange(imageInfo);

                    imageInfo = list;

                    Console.WriteLine("Calling StateHasChanged");

                    StateHasChanged();
                }
            });
        }

        public void Dispose()
        {
            idleClient.Unsubscribe(this);
        }
    }
}

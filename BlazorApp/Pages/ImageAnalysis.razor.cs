using BlazorApp.AzureComputerVision;
using BlazorApp.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorApp.Pages
{
    public partial class ImageAnalysis : IObserver<Data.ImageInfo>, IDisposable
    {
        public Guid ClientSessionId { get; } = Guid.NewGuid();

#nullable disable
        [Inject]
        private ILogger<ImageAnalysis> Logger { get; set; }

        [Inject]
        private IJSRuntime JS { get; set; }

        [Inject]
        private ImageInfoService ImageInfoService { get; set; }

        [Inject]
        private IdleClient idleClient { get; set; }
#nullable enable

        private SortedSet<Data.ImageInfo>? imageInfo;

        private long DeleteId;

        private int skip = 0;

        [Parameter]
        public string? SearchTerms { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var asyncQuery = ImageInfoService.GetImageInfoAsync(take: 15, skip: skip, SearchTerms);

            imageInfo = new SortedSet<Data.ImageInfo>();

            await foreach (var element in asyncQuery)
            {
                imageInfo.Add(element);

                StateHasChanged();
            }

            idleClient.Subscribe(this);
        }

        protected async Task LoadNext()
        {
            skip += 15;

            var newList = new SortedSet<Data.ImageInfo>(imageInfo?.AsEnumerable() ?? Array.Empty<Data.ImageInfo>());

            await foreach (var newImage in ImageInfoService.GetImageInfoAsync(take: 15, skip: skip, SearchTerms))
            {
                newList.Add(newImage);
            }

            imageInfo = newList;
        }

        protected void ConfirmDelete(long id, string title)
        {
            DeleteId = id;

            JS.InvokeAsync<bool>("confirmDelete", title);
        }

        protected async Task DeleteImageInfo()
        {
            await JS.InvokeAsync<bool>("hideDeleteDialog");

            await ImageInfoService.DeleteImageInfoAsync(DeleteId);

            await OnInitializedAsync();
        }

        protected async Task TriggerSearch()
        {
            imageInfo = null;

            skip = 0;

            imageInfo = new SortedSet<Data.ImageInfo>();

            await foreach (var image in ImageInfoService.GetImageInfoAsync(take: 15, skip: skip, SearchTerms))
            {
                imageInfo.Add(image);
            }
        }

        public void OnCompleted()
        {
            Logger.LogInformation("Observer has finisehd sending push-back notification");
        }

        public void OnError(Exception error)
        {
            Logger.LogError(error, error.Message);
        }

        public void OnNext(Data.ImageInfo value)
        {
            InvokeAsync(() =>
            {
                if (SearchTerms == null && skip == 0)
                {
                    var list = new SortedSet<Data.ImageInfo>(imageInfo?.AsEnumerable() ?? Array.Empty<Data.ImageInfo>());

                    list.Add(value);

                    imageInfo = list;

                    Console.Out.WriteLine("[OnNext] Calling StateHasChanged");

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

using Azure.AI.Vision.ImageAnalysis;

namespace BlazorApp.Model;

public class SerializableImageAnalysisResult
{
    /// <summary> The generated phrase that describes the content of the analyzed image. </summary>
    public CaptionResult? Caption { get; }
    /// <summary>
    /// The up to 10 generated phrases, the first describing the content of the whole image,
    /// and the others describing the content of different regions of the image.
    /// </summary>
    public DenseCaptionsResult? DenseCaptions { get; }
    /// <summary> Metadata associated with the analyzed image. </summary>
    public ImageMetadata? Metadata { get; }
    /// <summary> The cloud AI model used for the analysis. </summary>
    public string? ModelVersion { get; }
    /// <summary> A list of detected physical objects in the analyzed image, and their location. </summary>
    public ObjectsResult? Objects { get; }
    /// <summary> A list of detected people in the analyzed image, and their location. </summary>
    public PeopleResult? People { get; }
    /// <summary> The extracted printed and hand-written text in the analyze image. Also knows as OCR. </summary>
    public ReadResult? Read { get; }
    /// <summary>
    /// A list of crop regions at the desired as aspect ratios (if provided) that can be used as image thumbnails.
    /// These regions preserve as much content as possible from the analyzed image, with priority given to detected faces.
    /// </summary>
    public SmartCropsResult? SmartCrops { get; }
    /// <summary> A list of content tags in the analyzed image. </summary>
    public TagsResult? Tags { get; }
}
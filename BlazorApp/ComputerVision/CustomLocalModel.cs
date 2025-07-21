using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BlazorApp.ComputerVision;

public class CustomLocalModel
{
    private readonly InferenceSession? _session;

    public CustomLocalModel(string? modelPath)
    {
        if (!string.IsNullOrWhiteSpace(modelPath))
            _session = new InferenceSession(modelPath);
    }

    private readonly string[] categories = ["Bassin", "Dompeux", "Séparateur"]; // Remplacez "autre" par votre troisième catégorie

    public string ClassifyImage(byte[] imageBytes)
    {
        if (_session == null)
            return "Session not initialized, cannot classify image.";

        // Prétraitement de l'image
        var width = 1280;
        var height = 720;
        var tensor = new DenseTensor<float>(new float[1 * width * height * 3], [1, width, height, 3]);

        var image = Image.Load<Rgb24>(imageBytes);
        for (var y = 0; y < image.Height; y++)
        {
            var pixelSpan = image.GetPixelMemoryGroup()[0].Span.Slice(y * image.Width, image.Width);
            for (var x = 0; x < image.Width; x++)
            {
                tensor[0, 0, y, x] = pixelSpan[x].R;
                tensor[0, 1, y, x] = pixelSpan[x].G;
                tensor[0, 2, y, x] = pixelSpan[x].B;
            }
        }

        // Créer une entrée pour le modèle
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", tensor)
        };

        Console.WriteLine(_session.InputMetadata);
        Console.WriteLine(_session.OutputMetadata);

        // Effectuer la prédiction
        using var results = _session.Run(inputs, ["output"]);
        var output = results[0].AsTensor<float>();

        int predictedIndex = Array.IndexOf(output.ToArray(), output.Max());

        // Retourner le nom de la catégorie
        return categories[predictedIndex];
    }
}
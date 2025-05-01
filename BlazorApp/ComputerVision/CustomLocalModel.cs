using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace BlazorApp.ComputerVision;

public class CustomLocalModel
{
    private InferenceSession? _session;

    public CustomLocalModel(string? modelPath)
    {
        if (modelPath != null)
            _session = new InferenceSession(modelPath);
    }

    private string[] categories = ["Bassin", "Dompeux", "Séparateur"]; // Remplacez "autre" par votre troisième catégorie

    public string ClassifyImage(byte[] image)
    {
        if (_session == null)
            return "";

        // Prétraitement de l'image
        var width = 1280;
        var height = 720;
        var tensor = new DenseTensor<float>(new float[1 * width * height * 3], [1, width, height, 3 ]);

        // Créer une entrée pour le modèle
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", tensor)
        };

        Console.WriteLine(_session.InputMetadata);
        Console.WriteLine(_session.OutputMetadata);

        // Effectuer la prédiction
        using var results = _session.Run(inputs, ["output"]);
        var output = results.First().AsTensor<float>();

        int predictedIndex = Array.IndexOf(output.ToArray(), output.Max());

        // Retourner le nom de la catégorie
        return categories[predictedIndex];
    }
}
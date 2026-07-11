using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PlantScan.Models;
using PlantScan.Services;

namespace PlantScan.Controllers;

public class PlantController : Controller
{
    private const string ResultTempDataKey = "PlantResult";
    private readonly PlantIdService _plantIdService;

    public PlantController(PlantIdService plantIdService)
    {
        _plantIdService = plantIdService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Index(IFormFile? image, string? capturedImage)
    {
        PlantResult? result = null;

        if (image != null && image.Length > 0)
        {
            result = await _plantIdService.IdentifyPlantAsync(image);
        }
        else if (!string.IsNullOrEmpty(capturedImage))
        {
            result = await _plantIdService.IdentifyPlantFromBase64Async(capturedImage);
        }

        if (result == null)
        {
            ModelState.AddModelError(string.Empty, "Plant identification failed. Please try again.");
            return View();
        }

        TempData[ResultTempDataKey] = JsonConvert.SerializeObject(result);
        return RedirectToAction(nameof(Result));
    }

    [HttpGet]
    public IActionResult Result()
    {
        if (TempData[ResultTempDataKey] is not string serializedResult)
            return RedirectToAction(nameof(Index));

        var result = JsonConvert.DeserializeObject<PlantResult>(serializedResult);
        if (result == null)
            return RedirectToAction(nameof(Index));

        return View(result);
    }
}
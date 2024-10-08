using Microsoft.AspNetCore.Mvc;
using ABCRetail.Models;
using ABCRetail.ViewModel;
using ABCRetail.AzureBlobService.Interface;
using Microsoft.AspNetCore.Authorization;
using ABCRetail.AzureTableService.Interfaces;
using System.Net.Http;
using Newtonsoft.Json;

public class ProductController : Controller
{
    private readonly IProductService _productService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<ProductController> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _functionUrl = "http://localhost:7164/api/uploadfile";

    public ProductController(IProductService productService, IBlobStorageService blobStorageService, ILogger<ProductController> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _productService = productService;
        _blobStorageService = blobStorageService;
        _logger = logger;
        _httpClient = httpClient;
        _functionUrl = configuration["AzureFunction:UploadFileFunctionUrl"];
    }

    // GET: /Product
    // used by both the customer and the admin
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var products = await _productService.GetAllProductsAsync();

            // Convert Product to ProductViewModel
            var viewModel = products.Select(p => new ProductViewModel
            {
                Id = p.RowKey,
                ProductName = p.ProductName,
                Description = p.Description,
                Price = p.Price,
                StockLevel = p.StockLevel,
                ImageUrl = p.ImageUrl
            }).ToList();

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving products.");
            // Handle the error appropriately
            return View("Error"); // Return an error view or handle as needed
        }
    }


    // GET: /Product/Details
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        try
        {
            // Retrieve the product from the service
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            // Map the Product to ProductViewModel
            var productViewModel = new ProductViewModel
            {
                Id = product.RowKey,
                ProductName = product.ProductName,
                Description = product.Description,
                Price = product.Price,
                StockLevel = product.StockLevel,
                ImageUrl = product.ImageUrl
            };

            // Pass the ViewModel to the view
            return View(productViewModel);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"An error occurred while retrieving product details: {ex.Message}");
            return View();
        }
    }



    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult UploadImage()
    {
        return View(new UploadImageViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadImage(IFormFile imageFile)
    {
        if (imageFile != null && imageFile.Length > 0)
        {
            // Prepare the multipart form data content
            using (var content = new MultipartFormDataContent())
            {
                // Add the image file stream to the content
                var fileContent = new StreamContent(imageFile.OpenReadStream());
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(imageFile.ContentType);
                content.Add(fileContent, "file", imageFile.FileName);

                // Send POST request to the Azure Function (URL loaded from appsettings.json)
                var response = await _httpClient.PostAsync(_functionUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    // Get the URL returned by the Azure Function
                    //var imageUrl = await response.Content.ReadAsStringAsync();
                    var result = await response.Content.ReadAsStringAsync();
                    dynamic jsonResponse = JsonConvert.DeserializeObject(result);
                    string imageUrl = jsonResponse.Url;

                    // Save image URL to TempData for use in Create or Edit
                    TempData["ImageUrl"] = imageUrl;

                    // Check if we are editing or creating
                    if (TempData.ContainsKey("EditProductId"))
                    {
                        // Redirect to Edit action with the product ID
                        var productId = TempData["EditProductId"]?.ToString();
                        return RedirectToAction(nameof(Edit), new { id = productId });
                    }
                    else
                    {
                        // Redirect to the Create action
                        return RedirectToAction(nameof(Create));
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Failed to upload image to Azure Function.");
                }
            }

            //// Upload image to blob storage
            //string imageUrl = await _blobStorageService.UploadFileAsync(imageFile.FileName, imageFile.OpenReadStream());

            //// Save image URL to TempData for use in Create or Edit
            //TempData["ImageUrl"] = imageUrl;

            //// Check if we are editing or creating
            //if (TempData.ContainsKey("EditProductId"))
            //{
            //    // Redirect to Edit action with the product ID
            //    var productId = TempData["EditProductId"]?.ToString();
            //    return RedirectToAction(nameof(Edit), new { id = productId });
            //}
            //else
            //{
            //    // Redirect to the Create action
            //    return RedirectToAction(nameof(Create));
            //}
        }

        ModelState.AddModelError("", "Please upload a valid image.");
        return View();
    }


    // GET: /Product/Create
    // this will be used by the admin to add products
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        var imageUrl = TempData["ImageUrl"] as string;
        if (string.IsNullOrEmpty(imageUrl))
        {
            return RedirectToAction(nameof(UploadImage));
        }

        var model = new ProductViewModel
        {
            ImageUrl = imageUrl // Pass the image URL to the view
        };
        return View(model);
    }

    // POST: /Product/Create
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(ProductViewModel model)
    {
        if (string.IsNullOrEmpty(model.Id))
        {
            _logger.LogInformation("Generating a new RowKey (Id).");
            model.Id = await _productService.GetNextProductRowKeyAsync();

            // Reset ModelState to recognize the new Id
            ModelState.Clear();
            TryValidateModel(model); // Revalidate the model with the new Id
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Validation Error: The Rowkey field is required.");
            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
            {
                _logger.LogWarning("Validation Error: " + error.ErrorMessage);
            }
            return View(model);
        }

        try
        {
            var product = new Product
            {
                RowKey = model.Id,
                PartitionKey = "Product",
                ProductName = model.ProductName,
                Description = model.Description,
                Price = model.Price,
                StockLevel = model.StockLevel,
                ImageUrl = model.ImageUrl
            };

            await _productService.AddProductAsync(product);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating the product.");
            ModelState.AddModelError("", "An error occurred while creating the product.");
            return View(model);
        }
    }



    // GET: /Product/Edit
    // used by the admin
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(string id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
            return NotFound();

        var model = new ProductViewModel
        {
            Id = product.RowKey,
            ProductName = product.ProductName,
            Description = product.Description,
            Price = product.Price,
            StockLevel = product.StockLevel,
            ImageUrl = product.ImageUrl
        };

        // Save product ID in TempData for the image upload process
        TempData["EditProductId"] = id;

        return View(model);
    }



    // POST: /Product/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(ProductViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // Retrieve the existing product
                var product = await _productService.GetProductByIdAsync(model.Id);
                if (product == null)
                {
                    ModelState.AddModelError("", "Product not found.");
                    return View(model);
                }

                // Update product details
                product.ProductName = model.ProductName;
                product.Description = model.Description;
                product.Price = model.Price;
                product.StockLevel = model.StockLevel;

                // Check if there's a new image URL
                if (TempData.ContainsKey("ImageUrl"))
                {
                    product.ImageUrl = TempData["ImageUrl"].ToString();
                }

                // Update the product in the database
                await _productService.UpdateProductAsync(product);

                TempData["SuccessMessage"] = "Product updated successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the product.");
                ModelState.AddModelError("", "An error occurred while updating the product.");
            }
        }

        // Return to the Edit view with validation errors if the model state is not valid
        return View(model);
    }



    // GET: /Product/Delete
    // used by admin
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            // Retrieve the product by id
            var product = await _productService.GetProductByIdAsync(id);

            if (product == null)
            {
                // If the product doesn't exist, redirect to Not Found or a list page
                return RedirectToAction("NotFound", "Home");
            }

            // Return the delete confirmation view, passing the product
            return View(product);
        }
        catch (Exception ex)
        {
            // Log the error
            _logger.LogError(ex, "Error retrieving product with id {id} for deletion.", id);

            // Redirect to a generic error page or handle accordingly
            return RedirectToAction("Error", "Home");
        }
    }

}
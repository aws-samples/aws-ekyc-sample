using System;
using System.Threading.Tasks;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using ekyc_api.DataDefinitions;
using Microsoft.AspNetCore.Mvc;

namespace ekyc_api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : Controller
{
    [HttpGet]
    public async Task<SettingsDTO> GetSettings()
    {
        var dto = new SettingsDTO();
        var strParamName = Environment.GetEnvironmentVariable("RekognitionCustomLabelsProjectArnParameterName");
        var client = new AmazonSimpleSystemsManagementClient();
        var response = client.GetParameterAsync(new GetParameterRequest { Name = strParamName }
        ).GetAwaiter().GetResult();

        if (response.Parameter?.Value != "default") dto.RekognitionCustomLabelsProjectArn = response.Parameter?.Value;

        return dto;
    }

    [HttpPost]
    public async Task<StatusCodeResult> SaveSettings(SettingsDTO dto)
    {
        if (dto.RekognitionCustomLabelsProjectArn != null)
        {
            var strParamName = Environment.GetEnvironmentVariable("RekognitionCustomLabelsProjectArnParameterName");
            var client = new AmazonSimpleSystemsManagementClient();
            await client.PutParameterAsync(new PutParameterRequest
                { Name = strParamName, Value = dto.RekognitionCustomLabelsProjectArn, Overwrite = true });
        }

        return Ok();
    }
}
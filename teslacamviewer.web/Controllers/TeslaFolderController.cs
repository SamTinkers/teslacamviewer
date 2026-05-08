using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using teslacamviewer.web.Contracts;
using teslacamviewer.web.Helpers;
using teslacamviewer.web.Models;
using teslacamviewer.web.Services;

namespace teslacamviewer.web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeslaFolderController: ControllerBase
    {
        private readonly ITeslaPhysicalFolderRepository _teslaFolderRepository;
        private readonly IMapper _mapper;
        private readonly IConfiguration _config;
        
        public TeslaFolderController(
            ITeslaPhysicalFolderRepository teslaFolderRepository,
            IMapper mapper,
            IConfiguration config) {
            _teslaFolderRepository = teslaFolderRepository;
            _mapper = mapper;
            _config = config;
        }

        [Authorize]
        [HttpGet]
        public IActionResult GetTeslaFolders() {
            var x = _mapper.Map<List<TeslaFolderContract>>(_teslaFolderRepository.GetTeslaFolders());
            x.ForEach(tf => tf.TeslaClips = null);
            return Ok(x);
        }

        [Authorize]
        [HttpGet, Route("{folderType}/{folderName}")]
        public IActionResult GetTeslaFolder(string folderType, string folderName) {
            return Ok(_mapper.Map<TeslaFolderContract>(_teslaFolderRepository.GetTeslaFolder(folderName, folderType)));
        }

        [HttpGet, Route("{folderType}/{folderName}/{fileName}")]
        public IActionResult GetTeslaClip(string folderType, string folderName, string fileName) {
            // Pick MIME by extension. Chrome refuses to render application/octet-stream
            // in <video> elements; Firefox is more permissive about extension-vs-MIME.
            // Tesla clips are .mp4 (H.264/AAC). thumb.png lives next to them.
            var mime = fileName.EndsWith(".mp4", System.StringComparison.OrdinalIgnoreCase) ? "video/mp4"
                     : fileName.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ? "image/png"
                     : fileName.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase) ? "application/json"
                     : "application/octet-stream";
            return PhysicalFile(Path.Combine(_config["rootFolder"], folderType, folderName, fileName), mime, fileName, enableRangeProcessing: true);
        }

        [HttpGet, Route("get/thumbnail/{folderType}/{folderName}")]
        public async Task<IActionResult> GetThumbnail(string folderType, string folderName) {
            var stream = await _teslaFolderRepository.GetThumbnail(folderName, folderType);

            if (stream == null)
                return NotFound();

            return File(stream, "image/png", "thumb.png");
        }

        [Authorize]
        [HttpDelete, Route("{folderType}/{folderName}")]
        public IActionResult DeleteTeslaFolder(string folderType, string folderName) {
            _teslaFolderRepository.DeleteTeslaFolder(folderType, folderName);
            return Ok();
        }
    }
}
using HelpDeskPro.Dtos;
using HelpDeskPro.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDeskPro.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets/{ticketId:int}/comments")]
public class CommentsController(ICommentService comments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CommentDto>>> GetAll(int ticketId) =>
        Ok(await comments.GetAsync(ticketId, User.GetUserId(), User.GetRole()));

    [HttpPost]
    public async Task<ActionResult<CommentDto>> Add(int ticketId, CreateCommentDto dto)
    {
        var created = await comments.AddAsync(ticketId, dto, User.GetUserId(), User.GetRole());
        return StatusCode(201, created);
    }
}
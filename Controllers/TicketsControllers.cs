using HelpDeskPro.Dtos;
using HelpDeskPro.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDeskPro.Controllers;

[ApiController]
[Authorize]                       // every endpoint here needs a valid token
[Route("api/tickets")]
public class TicketsController(ITicketService tickets) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TicketDto>>> GetAll() =>
        Ok(await tickets.GetAllAsync(User.GetUserId(), User.GetRole()));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDto>> GetById(int id) =>
        Ok(await tickets.GetByIdAsync(id, User.GetUserId(), User.GetRole()));

    [HttpPost]
    public async Task<ActionResult<TicketDto>> Create(CreateTicketDto dto)
    {
        var created = await tickets.CreateAsync(dto, User.GetUserId(), User.GetRole());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TicketDto>> Update(int id, UpdateTicketDto dto) =>
        Ok(await tickets.UpdateAsync(id, dto, User.GetUserId(), User.GetRole()));

    [HttpPost("{id:int}/assign")]
public async Task<ActionResult<TicketDto>> Assign(int id, AssignTicketDto dto) =>
    Ok(await tickets.AssignAsync(id, dto, User.GetUserId(), User.GetRole()));

[HttpPost("{id:int}/status")]
public async Task<ActionResult<TicketDto>> ChangeStatus(int id, ChangeStatusDto dto) =>
    Ok(await tickets.ChangeStatusAsync(id, dto, User.GetUserId(), User.GetRole()));

[HttpGet("{id:int}/history")]
public async Task<ActionResult<List<HistoryDto>>> GetHistory(int id) =>
    Ok(await tickets.GetHistoryAsync(id, User.GetUserId(), User.GetRole()));
}
using System.ComponentModel.DataAnnotations;
using HelpDeskPro.Models;

namespace HelpDeskPro.Dtos;

public class CreateCommentDto
{
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Text { get; set; } = "";

    public bool IsInternal { get; set; }
}

public record CommentDto(int Id, string Text, bool IsInternal, string Author, Role AuthorRole, DateTime CreatedAt);
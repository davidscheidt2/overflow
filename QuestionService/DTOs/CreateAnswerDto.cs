using System.ComponentModel.DataAnnotations;

namespace QuestionService.DTOs;

public record CreateAnswerDto(    
    [Required] string Content 
);
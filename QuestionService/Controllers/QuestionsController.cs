using System.Security.Claims;
using Contracts;
using FastExpressionCompiler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;
using QuestionService.Services;
using Wolverine;

namespace QuestionService.Controllers;

[ApiController]
[Route("[controller]")]
public class QuestionsController(QuestionDbContext db, IMessageBus bus, TagService tagService) : Controller
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto dto)
    {
        if (!await tagService.AreTagsValidAsync(dto.Tags))
        {
            return BadRequest("Invalid Tags");
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name");
        
        if (userId is null || name is null) return BadRequest("Cannot get user details");
        
        var question = new Question
        {
            Title = dto.Title,
            Content = dto.Content,
            TagSlugs = dto.Tags,
            AskerId = userId,
            AskerDisplayName = name,
        };
        
        db.Questions.Add(question);
        await db.SaveChangesAsync();
        
        await bus.PublishAsync(new Contracts.QuestionCreated(
            question.Id,
            question.Title,
            question.Content,
            question.CreatedAt,
            question.TagSlugs));
        
        return Created($"/questions/{question.Id}", question);
    }
    
    [HttpGet]
    public async Task<ActionResult<List<Question>>> GetQuestions(string? tag)
    {
        var query = db.Questions.AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(q => q.TagSlugs.Contains(tag));
        }
        
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Question>> GetQuestion(string id)
    {
        var question = await db.Questions
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(q => q.Id == id);
        if (question is null) return NotFound();
        
        await db.Questions.Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ViewCount, x => x.ViewCount + 1));
        
        return question;
    }
    
    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateQuestion(string id, CreateQuestionDto dto)
    {
        var question = await db.Questions.FindAsync(id);
        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != question.AskerId) return Forbid();
        
        if (!await tagService.AreTagsValidAsync(dto.Tags))
        {
            return BadRequest("Invalid Tags");
        }

        question.Title = dto.Title;
        question.Content = dto.Content;
        question.TagSlugs = dto.Tags;
        question.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        
        await bus.PublishAsync(new QuestionUpdated(question.Id, question.Title, question.Content, 
            question.TagSlugs.AsArray()));
        
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteQuestion(string id)
    {
        var question = await db.Questions.FindAsync(id);
        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != question.AskerId) return Forbid();
        
        db.Questions.Remove(question);
        
        await db.SaveChangesAsync();

        await bus.PublishAsync(new QuestionDeleted(question.Id));
        
        return NoContent();
    }
    
    [Authorize]
    [HttpPost("{questionId}/answers")]
    public async Task<ActionResult<Answer>> CreateAnswer(string questionId, CreateAnswerDto dto)
    {
        var question = await db.Questions.FindAsync(questionId);
        if (question is null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name");
        
        if (userId is null || name is null) return BadRequest("Cannot get user details");
        
        var answer = new Answer
        {
            Content = dto.Content,
            UserId = userId,
            UserDisplayName = name,
            QuestionId = questionId
        };
        
        question.Answers.Add(answer);
        question.AnswerCount++;
        
        await db.SaveChangesAsync();

        await bus.PublishAsync(new Contracts.AnswerCountUpdated(
            questionId,
            question.AnswerCount));

        return Created($"/questions/{answer.QuestionId}/answers/{answer.Id}", answer);
    }

    [Authorize]
    [HttpPut("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> UpdateAnswer(string questionId, string answerId, CreateAnswerDto dto)
    {
        var answer = await db.Answers.FindAsync(answerId);
        if (answer is null) return NotFound();
        
        answer.Content = dto.Content;
        answer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{questionId}/answers/{answerId}")]
    public async Task<ActionResult> DeleteAnswer(string questionId, string answerId)
    {
        var answer = await db.Answers.FindAsync(answerId);

        var question = await db.Questions.FindAsync(questionId);
        if (question is null || answer is null) return NotFound();
        
        if (answer.QuestionId != question.Id || answer.Accepted) return BadRequest("Cannot delete this answer");

        db.Answers.Remove(answer);
        question.AnswerCount--;
        
        await db.SaveChangesAsync();

        await bus.PublishAsync(new Contracts.AnswerCountUpdated(
            questionId,
            question.AnswerCount));
        
        return NoContent();
    }
    
    [Authorize]
    [HttpPost("{questionId}/answers/{answerId}/accept")]
    public async Task<ActionResult> AnswerAccepted(string questionId, string answerId)
    {
        var answer = await db.Answers.FindAsync(answerId);
        var question = await db.Questions.FindAsync(questionId);
        if (question is null || answer is null) return NotFound();

        if (answer.QuestionId != questionId || question.HasAcceptedAnswer) return BadRequest("Cannot accept this answer");
      
        answer.Accepted = true;
        question.HasAcceptedAnswer = true;
        
        await db.SaveChangesAsync();

        await bus.PublishAsync(new Contracts.AnswerAccepted(
            questionId));

        return NoContent();
    }

}
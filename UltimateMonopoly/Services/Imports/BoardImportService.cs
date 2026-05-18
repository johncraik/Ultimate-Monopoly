using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using UltimateMonopoly.Helpers;
using UltimateMonopoly.Models;
using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Models.ImportModels;

namespace UltimateMonopoly.Services.Imports;

public class BoardImportService
{
    private readonly FilePathProvider _filePathProvider;
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private readonly string _boardFileName;

    public BoardImportService(FilePathProvider filePathProvider, 
        IConfiguration config,
        IRepositoryManager repos,
        IUserInfo userInfo)
    {
        _filePathProvider = filePathProvider;
        _repos = repos;
        _userInfo = userInfo;

        var fileName = config["Imports:Board"];
        if(string.IsNullOrEmpty(fileName))
            throw new ArgumentNullException(nameof(fileName), "Board file name is not set in configuration");
        
        _boardFileName = $"{fileName}{FilePathProvider.ConfigFileType}";
    }

    public async Task<Board> ImportDefaultBoard()
    {
        var path = _filePathProvider.GetFilePath(FilePathProvider.ConfigType.Board);
        var fileText = await _filePathProvider.ReadFileAsync(path, _boardFileName);
        
        var importResult = JsonConvert.DeserializeObject<List<BoardSpaceJsonImport>>(fileText);
        if(importResult == null)
            throw new InvalidOperationException("Failed to deserialise board data from file");

        var spaces = new List<BoardSpace>();
        foreach (var import in importResult)
        {
            var space = new BoardSpace(import);
            
            if(import.Rents != null)
            {
                var result = space.SetRents(import.Rents);
                if(!result)
                    throw new InvalidOperationException("Failed to set rents for board space");
            }
            else if(space.Index.IsProperty() || space.Index.IsStation() || space.Index.IsUtility())
                throw new InvalidOperationException("Rents must be provided for property, station, or utility spaces");
            
            spaces.Add(space);
        }
        
        return spaces.Count != IndexHelper.BoardSize 
            ? throw new InvalidOperationException($"Board must have {IndexHelper.BoardSize} spaces") 
            : new Board(spaces);
    }

    public async Task<List<Board>> GetCustomBoards(Board defaultBoard, string? userId = null)
    {
        userId ??= _userInfo.UserId;
        var customBoards = await _repos.GetRepository<CustomBoard>()
            .AsQueryable()
            .Include(b => b.Spaces)
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .ToListAsync();

        var boards = (from customBoard in customBoards
            let spaces = (from defaultSpace in defaultBoard.Spaces
                let customSpace = customBoard.Spaces.FirstOrDefault(s => s.Index == defaultSpace.Index)
                select customSpace == null
                    ? defaultSpace
                    : new BoardSpace(customSpace, defaultSpace)).ToList()
            select new Board(customBoard.Name, spaces)).ToList();
        
        return boards.Any(b => b.Spaces.Count != IndexHelper.BoardSize) 
            ? throw new InvalidOperationException($"All custom boards must have {IndexHelper.BoardSize} spaces") 
            : boards;
    }
}
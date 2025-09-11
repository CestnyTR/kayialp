using System.Collections.Generic;
using System.Threading.Tasks;

namespace kayialp.Services
{
    public record LangInfo(int Id, string Code);

    public interface ILangResolver
    {
        Task<IReadOnlyList<LangInfo>> GetAllAsync();
        Task<LangInfo> GetSourceAsync(string sourceCode = "tr"); // admin girişi TR
    }
}

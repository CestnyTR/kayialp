using System.Threading.Tasks;

namespace kayialp.Services
{
    public interface ITranslationService
    {
        Task<string> TranslateAsync(string text, string sourceLangCode, string targetLangCode);
    }
}

using kayialp.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace kayialp.Services
{
    public class LangResolver : ILangResolver
    {
        private readonly kayialpDbContext _db;
        private readonly IMemoryCache _cache;

        public LangResolver(kayialpDbContext db, IMemoryCache cache)
        {
            _db = db; _cache = cache;
        }

        public async Task<IReadOnlyList<LangInfo>> GetAllAsync()
        {
            if (_cache.TryGetValue("langs_all", out IReadOnlyList<LangInfo>? cached) && cached is not null)
                return cached;

            var list = await _db.Langs
                .AsNoTracking()
                .OrderBy(l => l.Id)
                .Select(l => new LangInfo(l.Id, l.LangCode))
                .ToListAsync();

            _cache.Set("langs_all", list, TimeSpan.FromMinutes(10));
            return list;
        }

        public async Task<LangInfo> GetSourceAsync(string sourceCode = "tr")
        {
            var langs = await GetAllAsync();
            var tr = langs.FirstOrDefault(l => l.Code.StartsWith(sourceCode, StringComparison.OrdinalIgnoreCase));
            if (tr is default(LangInfo)) throw new InvalidOperationException($"Language '{sourceCode}' not found.");
            return tr;
        }
    }
}

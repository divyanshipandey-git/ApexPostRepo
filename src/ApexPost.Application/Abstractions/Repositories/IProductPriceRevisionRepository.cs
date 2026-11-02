using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Abstractions.Repositories;

public interface IProductPriceRevisionRepository
{
    Task<int> SetCategoryPriceAsync(
        ProductCategory category,
        Money unitPrice,
        CancellationToken cancellationToken = default);
}

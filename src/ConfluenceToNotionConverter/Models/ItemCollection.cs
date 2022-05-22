using ConfluenceToNotionConverter.Contracts;
using System.Collections.ObjectModel;

namespace ConfluenceToNotionConverter.Models
{
    internal sealed class ItemCollection<TUniqueEntity> : KeyedCollection<string, TUniqueEntity>
		where TUniqueEntity : IUniqueEntity
	{
		protected override string GetKeyForItem(TUniqueEntity item) => item.Id;
	}
}

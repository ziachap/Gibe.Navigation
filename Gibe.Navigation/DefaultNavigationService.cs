﻿using System;
using System.Collections.Generic;
using System.Linq;
using Gibe.Caching.Interfaces;
using Gibe.Navigation.Models;

namespace Gibe.Navigation
{
	public class DefaultNavigationService<T> : INavigationService<T> where T : INavigationElement
	{
		private readonly ICache _cache;
		private readonly IEnumerable<INavigationProvider<T>> _providers;
		
		private const string CacheKey = "navigation";

		public DefaultNavigationService(ICache cache, IEnumerable<INavigationProvider<T>> providers)
		{
			_cache = cache;
			_providers = providers;
		}

		public Navigation<T> GetNavigation()
		{
			return GetNavigation(null);
		}

		public Navigation<T> GetNavigation(string currentUrl)
		{
			List<T> navElements;

			if (_cache.Exists(CacheKey))
			{
				navElements = Clone(_cache.Get<List<T>>(CacheKey)).ToList();
			}
			else
			{
				navElements = _providers.OrderBy(p => p.Priority).SelectMany(p => p.GetNavigationElements()).ToList();
				_cache.Add(CacheKey, navElements, new TimeSpan(0, 10, 0));
			}

			SelectActiveTree(navElements, currentUrl);

			return new Models.Navigation<T>
			{
				Items = Visible(navElements)
			};
		}


		public SubNavigationModel<T> GetSubNavigation(string url)
		{
			var matchingSideNavigation = _providers
				.OrderBy(x => x.Priority)
				.Select(x => x.GetSubNavigation(url))
				.SingleOrDefault(x => x != null);

			if (matchingSideNavigation == null) return null;

			SelectActiveTree(matchingSideNavigation.NavigationElements.ToList(), url);

			return new SubNavigationModel<T>
			{
				SectionParent = matchingSideNavigation.SectionParent,
				NavigationElements = Visible(matchingSideNavigation.NavigationElements.ToList())
			};
		}

		private bool SelectActiveTree(List<T> elements, string currentUrl)
		{
			if (currentUrl != null)
			{
				foreach (var navigationElement in elements)
				{
					if (navigationElement.IsConcrete && (navigationElement.Url.TrimEnd('/') == currentUrl.TrimEnd('/')))
					{
						navigationElement.IsActive = true;
						return true;
					}

					navigationElement.IsActive = SelectActiveTree(navigationElement.Items.Select(i => (T)i).ToList(), currentUrl);
					if (navigationElement.IsActive)
					{
						return true;
					}
				}
			}

			return elements.Any(n => n.IsActive);
		}

		private IEnumerable<T> Clone(IEnumerable<T> elements)
			=> elements.Select(e => (T)e.Clone()).ToList();

		private IEnumerable<T> Visible(List<T> elements)
			=> elements.Where(e => e.IsVisible).Select(e => (T)e.Clone());
	}
	
}

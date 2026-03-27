using Core.DTOs;
using Microsoft.AspNetCore.Mvc;
using Repositories.Interfaces;
using Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace App.Controllers
{
    public class CollectionsController : Controller
    {
        private readonly CollectionService _collectionService;
        private readonly SetService _setService;
        private readonly IUnitOfWork _unitOfWork;

        public CollectionsController(
            CollectionService collectionService,
            SetService setService,
            IUnitOfWork unitOfWork)
        {
            _collectionService = collectionService;
            _setService = setService;
            _unitOfWork = unitOfWork;
        }

        // 1. Список колекцій
        public async Task<IActionResult> Index()
        {
            var collections = (await _collectionService.GetCollectionsByUserIdAsync()).ToList();

            foreach (var item in collections)
            {
                // Фікс кількості сетів
                if (item.Id.HasValue)
                {
                    var setsInCollection = await _unitOfWork.Sets.GetAllAsync(
                        filter: s => s.Collections.Any(c => c.Id == item.Id.Value)
                    );
                    item.SetsCount = setsInCollection.Count();
                }

                // ФІКС ДАТ: Якщо в базі порожньо (01.01.0001), ставимо поточний час
                if (item.CreatedAt == DateTime.MinValue) item.CreatedAt = DateTime.Now;
                if (item.LastUpdatedAt == DateTime.MinValue) item.LastUpdatedAt = DateTime.Now;
            }
            return View(collections);
        }

        // 2. Деталі колекції
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var collectionDetail = await _collectionService.GetCollectionByIdAsync(id);
                return View(collectionDetail);
            }
            catch
            {
                return RedirectToAction(nameof(Index));
            }
        }

        // 3. Створення (виправляємо дати перед записом)
        [HttpPost]
        public async Task<IActionResult> Create(CollectionDTO collectionDto)
        {
            if (ModelState.IsValid)
            {
                // Встановлюємо реальний час перед відправкою в сервіс
                collectionDto.CreatedAt = DateTime.Now;
                collectionDto.LastUpdatedAt = DateTime.Now;
                await _collectionService.CreateCollectionAsync(collectionDto);
            }
            return RedirectToAction(nameof(Index));
        }

        // 4. Оновлення (стає актуальний час)
        [HttpPost]
        public async Task<IActionResult> Update(CollectionDTO collectionDto)
        {
            if (ModelState.IsValid && collectionDto.Id.HasValue)
            {
                // Оновлюємо тільки дату зміни
                collectionDto.LastUpdatedAt = DateTime.Now;
                await _collectionService.UpdateCollectionAsync(collectionDto);
            }
            return RedirectToAction(nameof(Index));
        }

        // 5. Видалення
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _collectionService.DeleteCollectionAsync(id);
            return RedirectToAction(nameof(Index));
        }

        // 6. Видалення сету з папки
        [HttpPost]
        public async Task<IActionResult> RemoveSet(int setId, int collectionId)
        {
            await _setService.RemoveSetFromCollectionAsync(setId, collectionId);

            var collection = await _unitOfWork.Collections.GetByIdAsync(collectionId);
            if (collection != null)
            {
                // Прибираємо UtcNow, ставимо Now для синхронізації з твоїм часом
                collection.UpdatedAt = DateTime.Now;
                await _unitOfWork.SaveChangesAsync();
            }

            return RedirectToAction("Details", new { id = collectionId });
        }
    }
}
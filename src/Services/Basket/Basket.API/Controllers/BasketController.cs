using AutoMapper;
using Basket.API.Entities;
using Basket.API.GrpcServices;
using Basket.API.Repositories;
using EventBus.Messages.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;

namespace Basket.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class BasketController : ControllerBase
    {
        private readonly IBasketRepository repository;
        private readonly DiscountGrpcService discountGrpcService;
        private readonly IMapper mapper;
        private readonly IPublishEndpoint publishEndpoint;

        public BasketController(IBasketRepository repository, DiscountGrpcService discountGrpcService, IMapper mapper,
            IPublishEndpoint publishEndpoint)
        {
            this.repository = repository;
            this.discountGrpcService = discountGrpcService;
            this.mapper = mapper;
            this.publishEndpoint = publishEndpoint;
        }

        [HttpGet("{userName}", Name = "GetBasket")]
        [ProducesResponseType(typeof(ShoppingCart), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ShoppingCart>> GetBasket(string userName)
        {
            var basket = await repository.GetBasket(userName);

            return Ok(basket ?? new ShoppingCart(userName));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ShoppingCart), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ShoppingCart>> UpdateBasket([FromBody] ShoppingCart basket)
        {
            foreach (var item in basket.Items)
            {
                var coupon = await discountGrpcService.GetDiscount(item.ProductName);
                item.Price -= coupon.Amount;
            }

            return Ok(await repository.UpdateBasket(basket));
        }

        [HttpDelete("{userName}", Name = "DeleteBasket")]
        [ProducesResponseType(typeof(void), (int)HttpStatusCode.OK)]
        public async Task DeleteBasket(string userName)
        {
            await repository.DeleteBasket(userName);
        }

        [HttpPost]
        [Route("[action]")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Checkout([FromBody] BasketCheckout basketCheckout)
        {

            // get existing basket with total price
            // create basket checkout event and set totalprice on basketCheckout eventMessage
            // send checkout event to the rabbit mq
            // remove the basket 

            // get existing basket with total price
            var basket = await repository.GetBasket(basketCheckout.UserName);
            if (basket == null)
            {
                return BadRequest();
            }

            //send checkout event to rabbitmq
            var eventMessage = mapper.Map<BasketCheckoutEvent>(basketCheckout);
            eventMessage.TotalPrice = basket.TotalPrice;
            await publishEndpoint.Publish(eventMessage);

            //remove basket
            await repository.DeleteBasket(basket.UserName);

            return Accepted();
        }

    }
}

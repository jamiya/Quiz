using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Data.Entity;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Description;
using Quiz.Models;
using System.Web;

namespace Quiz.Controllers
{
    [Authorize]
    public class QuizController : ApiController
    {
        private Context db = new Context();

        private int currentPage; // одоогын хуудас
        private DateTime ExpireDate; // дуусах огноо
        private Response response = new Response(); // get-р ирсэн хүсэлтэнд буцаах үр дүн
        private int pageSize = 5; // хуудаслалтын хэмжээ


        public QuizController()
        {
            var session = HttpContext.Current.Session; 

            if (session != null)
            {
                if (session["currentPage"] == null)
                {
                    session["currentPage"] = 1;
                }

                this.currentPage = (int)session["currentPage"];
                this.response.currentPage = this.currentPage;
            }




            if (this.currentPage == 1)
            {
                session["ExpireDate"] = DateTime.Now.AddMinutes(1);  // одоогын хуудас 1 байвал тест эхэллээ гэж үзээд доогын огноон дээр хугацааг нэмээд дуусах огноонд оноож байна 
            //    session["Started"] = true;
            }

            this.ExpireDate = (DateTime)session["ExpireDate"];
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.db.Dispose();
            }

            base.Dispose(disposing);
        }


        private async Task<Object[]> GetScoreAsync()
        {

            var answers = await (from a in db.Answers
                                 join o in db.Options on a.OptionId equals o.Id
                                 join q in db.Questions on a.QuestionId equals q.Id
                                 where a.UserId == User.Identity.Name && a.ExpireDate == this.ExpireDate
                                 select new
                                 {
                                     q = q.Title,
                                     o = o.Title,
                                     c = a.isCorrect
                                 }).ToArrayAsync();       



     //        answer[] answers = await this.db.answers.j    // хариулт хүснэгтээс хэрэглэгчийн нэр болон дуусах огноогоор хариултуудыг шүүж авч байна
     //        .where(a => a.userid == user.identity.name && a.expiredate == this.expiredate)
     //        .toarrayasync();

            return answers;
        }


        private async Task<Question[]> NextQuestionsAsync()
        {

            var questionsCount = await this.db.Questions.CountAsync(); // Нийт асуултын тоог авч байна

            this.response.totalPage = (int) Math.Ceiling((decimal) questionsCount / this.pageSize); // Хуудасны тоог авч байна

            if (this.currentPage > this.response.totalPage)   // Одоогын хуудас нийт хуудаснаас хэтэрсэн бол тест дууссан гэж үзнэ
            {
                await this.finish();

               var session = HttpContext.Current.Session;

               session["currentPage"] = null;

                return null;
            }

            var skip = this.pageSize * (this.currentPage - 1);  // Бөгөлсөн буюу алгасан асуултын тоо

            return await this.db.Questions.OrderBy(q => q.Id)
                                .Skip(skip)
                                .Take(this.pageSize).ToArrayAsync();   // Дараагын асуултуудыг авж байна
            
        }


        public async Task<bool> finish()
        {
               this.response.finished = true;
               this.response.countDown = 0;

             return true;
        }



        // GET api/Quiz/Get
        [ResponseType(typeof(Response))]
        public async Task<IHttpActionResult> Get()
        {

            this.response.Score = await this.GetScoreAsync(); // хариултуудыг буцаах обьектод хадгалж байна

                    if (this.ExpireDate < DateTime.Now){   // тестийн огноо дууссан эсэхийг шалгаж байна

                      var session = HttpContext.Current.Session;

                      session["currentPage"] = null;
                      
                      await this.finish();

                      this.response.Expired = true;      
                                                               
                      return this.Ok(this.response);
                   }

            this.response.questions = await this.NextQuestionsAsync();

            TimeSpan time = this.ExpireDate.Subtract(DateTime.Now);
            this.response.countDown = (int)time.TotalSeconds; // Хичнээн хугацаа үлдсэнийг секундээр авж байна

            return this.Ok(this.response);
        }
        // Хариултыг хадгалаад зөв бурууг буцаана 
        private async Task<bool> StoreAsync(Answer answer)
        {
            // Бөгөлсөн хариултыг шүүж авна 
            var selectedOption = await this.db.Options.FirstOrDefaultAsync(o => o.Id == answer.OptionId && o.QuestionId == answer.QuestionId);

            // Хариулт зөв эсэхийг тодорхойлно
            answer.isCorrect = selectedOption.IsCorrect;

            // Answer Хүснэгтэнд бичлэг нэмж байна
            this.db.Answers.Add(answer);

            // Баазад өөрчлөлтийг хадгалж байна 
            await this.db.SaveChangesAsync();

            return selectedOption.IsCorrect;
        }

        // POST api/Quiz/Post 
        [ResponseType(typeof(Answer[]))]
        public async Task<IHttpActionResult> Post(Answer[] answers)
        {
            if (!ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            // Хариултууд 5 5 аар орж ирэх учир хариулт бүрт хэрэглэгчийн нэр дуусах огноог оноогоод хадгалах фунцкруу дамжуулна
            foreach (Answer answer in answers)
            {
                answer.UserId = User.Identity.Name;
                answer.ExpireDate = this.ExpireDate;

                var isCorrect = await this.StoreAsync(answer);

            }

            var session = HttpContext.Current.Session;

            session["currentPage"] = this.currentPage + 1; // Тухайн хуудсыг хариулсан гэж үзээд дараагын хуудасны дугаарын session-д хадгалж байна
                                                           // Энэ нь хэрэглэгч аль хуудсан дээр явж байгааг мэдэх боломжыг олгоно

            return this.Ok(true);
        }


        private async Task DeleteAnswerAsync()
        {

            Answer[] answers = await this.db.Answers
            .Where(a => a.UserId == User.Identity.Name)
            .ToArrayAsync();

            foreach (Answer answer in answers)
            {
                this.db.Answers.Remove(answer);
            }

        }




    }


    public class Response
    {
        public int totalPage { get; set; }
        public int currentPage { get; set; }
        public Question[] questions { get; set; }
        public int progress { get; set; }
        public Object[] Score { get; set; }
        public bool Expired { get; set; }
        public bool Started { get; set; }
        public int countDown { get; set; }
        public bool finished { get; set; }

        public Response()
        {
            this.Expired = false;
            this.Started = false;
            this.finished = false;
        }
    }
}

#r "Microsoft.WindowsAzure.Storage"
using Microsoft.WindowsAzure.Storage.Table;
using System.Net;

//this function will get all the urls if no parameters are passed
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, IQueryable<StateEntity> statusTable,TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");
   // Get request body
   if(req.Content==null){
       return req.CreateResponse(HttpStatusCode.BadRequest,"The provided data was empty");
   }
    dynamic data = await req.Content.ReadAsAsync<object>();
    if(data==null){
        log.Info("data is null");
        return req.CreateResponse(HttpStatusCode.BadRequest,"The provided data was empty");
    }else{
    string draw = data?.draw;
    string order = data?.order[0]["column"];
    string orderDir = data?.order[0]["dir"];
    int startRec = Convert.ToInt32(data?.start);
    int pageSize = Convert.ToInt32(data?.length);
    
    //searchFields
    string urlName = data?.columns[0]["search"]["value"];
    string url =  data?.columns[1]["search"]["value"];
    IEnumerable<StateEntity> entities = statusTable.Where(p => p.PartitionKey == "statuses" && p.Status!="OK").ToList();
    /*Here we are allowing only one sorting at time. orderDir will hold asc or desc for sorting the column. */   

#region Sorting  
  
    //Sorting     
                switch (order)  
                {  
                    case "1":  
                        entities = orderDir.Equals("DESC", StringComparison.CurrentCultureIgnoreCase) ? entities.OrderByDescending(p => p.Url) : entities.OrderBy(p => p.Url);  
                        break;  
                    case "2":  
                        entities = orderDir.Equals("DESC", StringComparison.CurrentCultureIgnoreCase) ? entities.OrderByDescending(p => p.UrlName) : entities.OrderBy(p => p.UrlName);  
                        break;  
                    default:  
                       entities = entities.OrderByDescending(p => p.UrlName);  
                        break;  
                }
 
#endregion    
    //search filter conditions for paging
    if (!string.IsNullOrEmpty(urlName) && !string.IsNullOrWhiteSpace(urlName))  
    {  
        entities= entities.Where(e=>e.UrlName != null && e.UrlName.ToLower().Contains(urlName.ToString()));
    }
    if (!string.IsNullOrEmpty(url) && !string.IsNullOrWhiteSpace(url))  
    {  
        entities= entities.Where(e=>e.Url != null && e.Url.ToLower().Contains(url.ToString()));
    }


#region Pagination

    var filteredEntities = entities
                        .Skip(startRec)
                        .Take(pageSize).ToList()
                            .Select(e=>new StateEntity(){
                                UrlName = e.UrlName,
                                Url = e.Url,
                                Status = e.Status,
                                Description = e.Description,
                                Date = e.Date

                                }).ToList();

    
    if(filteredEntities==null)
        filteredEntities= new List<StateEntity>();
#endregion
    int recordsCount = entities.ToList().Count;
    var response = new {
                    draw = Convert.ToInt32(draw),  
                    recordsTotal = recordsCount,  
                    recordsFiltered = recordsCount,  
                    data =  filteredEntities
    };
    return urlName == null
        ? req.CreateResponse(HttpStatusCode.OK,response)
        : req.CreateResponse(HttpStatusCode.OK, statusTable.Where(p => p.PartitionKey == "statuses" && p.RowKey==urlName).ToList().FirstOrDefault());
    }

}

public class StateEntity:TableEntity
{
    
    public string UrlName {get;set;}
    public string Url {get;set;}
    public string Status {get;set;}
    public string Description {get;set;}
    public DateTime Date {get;set;}
}







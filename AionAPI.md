




api/schedules/active


group=foo/bar
orderBy=schedule|name


GET: api/workflows?enabled=false
GET: api/workflows/active -> scheduled workflows
GET: api/workflows/next/3/hours -> workflows going to be executed
GET: api/workflows/next/3/times -> workflows going to be executed

POST: api/workflows
{
    "name": "foo/bar/baz.json",
    "commands": []
}



initial
var doggos = [
  {
    name: 'Evie'
    age: 5
    interests: ['Ball', 'Frisbee']
  }
  {
    name: 'Casper'
    age: 3
    interests: ['Other dogs']
  }
  {
    name: 'Indy'
    age: 2
    interests: ['Butter']
  }
  {
    name: 'Kira'
    age: 8
    interests: ['Rubs']
  }
]

// samples
// output dogNames array = map(doggos, dog => dog.name)
// output sayHi array = map(doggos, dog => 'Hello ${dog.name}!')

// var ages = map(doggos, dog => dog.age)
// output totalAge int = reduce(ages, 0, (cur, prev) => cur + prev)

// output doggosByAge array = sort(doggos, (a, b) => a.age < b.age)

// output oldBois array = filter(doggos, dog => dog.age >= 5)
// output interests array = flatten(map(doggos, dog => dog.interests))

// limitations
// var foo = i => i * 2

// resource loop referencing has limitations
// var accNames = map(range(0, 10), i => 'acc${i}')
// resource accs 'Microsoft.Storage/storageAccounts@2021-09-01' existing = [for name in accNames: {
//   name: name
// }]
// var accProps = map(accs, acc => acc.properties.primaryEndpoints)
// var accProps2 = map(range(0, 10), i => accs[i].properties.primaryEndpoints)

// property iteration 
// var endpoints = map(items(accs[0].properties.primaryEndpoints), endpoint => endpoint.value)

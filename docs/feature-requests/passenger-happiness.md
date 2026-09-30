I want to add three properties to persons/passengers:

- Happiness
- Preferred ride intensity
- Nausea

All properties have a max value between 0 and 100 where 0 is the lowest, and 100 is the highest. 0 happiness, means that the person is very sad, while 100 means extremely happy. The same counts for ride intensity and the nausea rating.

When people enter the queue, they will be assigned a start happyness value that must be a random between 65 and 85, so fairly happy to quite happy. The preferred ride intensity is randomized between 50 and 100. The nausea rating is always set to 0. A low preferred ride intensity means that these people become more happy when the ride speed remains slow. For a high preferred intensity, people get happier when the ride spins fast. When people prefer a low ride intensity, but the ride in reality spins very fast, the nausea value raises.

- When people sit in the queue for longer than 5 minutes, their happiness will decrease exponentially
- When people prefer 50% ride intensity, they become significantly happier when the ride hits around 50% of the max allowed G-forces
- When people prefer 100% ride intensity, they become significantly happier when the ride touches the max allowed G-forces
- When people prefer a low ride intensity (say 50%), but the ride is very intense (a difference of at least 30%), then people get nauseous exponentially
- When a gondola experiences the max allowed G-Forces for longer than 1 second, its passengers gain 25% nausea rating every time this occurs

Add a new Rider Experience panel in the right column, under the Gondolas panel. It shows four progress bars:
- Queue happiness
- Rider happiness
- Rider intensity
- Rider nausea

The Queue happiness shows the average happiness of all people in the queue
The rider happiness shows the average happiness of all people on the ride
The rider intensity shows the average preferred intensity of all people on the ride
The rider nausea shows the average nausea rating of all people on the ride